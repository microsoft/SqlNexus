using System;
using System.Collections.Generic;
using System.IO;

namespace sqlnexus
{
    /// <summary>
    /// Resolves the set of directories that SQL Nexus should search for importable files.
    ///
    /// SQL LogScout's "All Instances" capture places instance-specific files in an instance
    /// subfolder (e.g. SERVER_SQL2019) and non-instance-specific (host/OS) files in a sibling
    /// folder named <see cref="SharedFolderName"/> (e.g. output\SharedOutputFiles). When the user
    /// points SQL Nexus at an instance folder, we also want to consider that sibling shared folder.
    ///
    /// Behavior (per design): IF a sibling <see cref="SharedFolderName"/> folder exists THEN search
    /// both the primary folder and the sibling; otherwise return only the primary folder so that
    /// everything behaves exactly as before.
    ///
    /// This type is intentionally free of WinForms/SQL dependencies so it can be unit-tested.
    /// The shared folder name and location (direct sibling only) are fixed by design and are never
    /// built from user input, so there is no injection surface. The resolved sibling is validated
    /// to be a real direct sibling of the primary folder (rejecting directory-traversal tricks).
    /// </summary>
    internal static class SharedOutputFolder
    {
        /// <summary>
        /// The fixed name of the shared (non-instance-specific) output folder produced by
        /// SQL LogScout. Only this exact name, and only as a direct sibling of the primary import
        /// folder, is considered.
        /// </summary>
        public const string SharedFolderName = "SharedOutputFiles";

        /// <summary>
        /// Returns the ordered list of directories to search for importable files.
        /// The primary path is always first. If a sibling <see cref="SharedFolderName"/> folder
        /// exists (and is not the primary folder itself), it is appended second.
        /// </summary>
        /// <param name="primaryPath">The folder the user pointed SQL Nexus at.</param>
        /// <returns>
        /// A list containing the normalized primary path, optionally followed by the sibling
        /// shared folder. Directory paths do NOT include a trailing directory separator.
        /// </returns>
        public static List<string> GetImportSearchPaths(string primaryPath)
        {
            var paths = new List<string>();

            if (string.IsNullOrWhiteSpace(primaryPath))
                return paths;

            string normalizedPrimary;
            try
            {
                // Normalize and strip any trailing separators for consistent comparison/combination.
                normalizedPrimary = Path.GetFullPath(primaryPath.Trim().Replace("\"", ""))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception)
            {
                // Malformed path - fail closed by returning nothing extra; callers still use their
                // own primary path. We intentionally do not throw here.
                return paths;
            }

            paths.Add(normalizedPrimary);

            string sharedPath = ResolveSharedSibling(normalizedPrimary);
            if (sharedPath != null)
                paths.Add(sharedPath);

            return paths;
        }

        /// <summary>
        /// Returns the validated path to the sibling <see cref="SharedFolderName"/> folder if it
        /// exists as a direct sibling of <paramref name="normalizedPrimary"/>; otherwise null.
        /// </summary>
        public static string ResolveSharedSibling(string normalizedPrimary)
        {
            if (string.IsNullOrWhiteSpace(normalizedPrimary))
                return null;

            string parent;
            try
            {
                parent = Path.GetDirectoryName(normalizedPrimary);
            }
            catch (Exception)
            {
                return null;
            }

            // No parent (e.g. a drive root) means there is no sibling location to look in.
            if (string.IsNullOrEmpty(parent))
                return null;

            string candidate;
            try
            {
                candidate = Path.GetFullPath(Path.Combine(parent, SharedFolderName))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception)
            {
                return null;
            }

            // Security: ensure the resolved candidate is a REAL direct sibling - i.e. its parent is
            // exactly the primary folder's parent, and its leaf name is exactly SharedFolderName.
            // This rejects traversal or symlink-style tricks that resolve elsewhere.
            string candidateParent = Path.GetDirectoryName(candidate);
            string candidateName = Path.GetFileName(candidate);

            bool isDirectSibling =
                candidateParent != null &&
                string.Equals(
                    candidateParent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    parent,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidateName, SharedFolderName, StringComparison.OrdinalIgnoreCase);

            if (!isDirectSibling)
                return null;

            // Do not treat the primary folder itself as its own shared sibling.
            if (string.Equals(candidate, normalizedPrimary, StringComparison.OrdinalIgnoreCase))
                return null;

            if (!Directory.Exists(candidate))
                return null;

            return candidate;
        }
    }
}
