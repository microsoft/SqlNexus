using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NexusInterfaces;

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
    /// The primary path comes from the user, but the shared folder NAME is a fixed constant
    /// (<see cref="SharedFolderName"/>) and is never built from user input. The resolved sibling is
    /// simply that fixed name combined with the primary folder's parent, and it is only returned
    /// after <see cref="Directory.Exists(string)"/> confirms the directory is actually present. The
    /// resolved path is used only to enumerate files for import - it is never used to build a SQL
    /// command or shell string - so path resolution here is not itself an injection surface.
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
        /// Logs a diagnostic message to the silent (file-only) log when a global logger is available.
        /// This type is used from unit tests where <see cref="Util.Logger"/> may be null, so logging
        /// is best-effort and never throws - failures fall back to <see cref="Debug.WriteLine(string)"/>.
        /// </summary>
        private static void LogSilent(string message)
        {
            try
            {
                if (Util.Logger != null)
                    Util.Logger.LogMessage(message, MessageOptions.Silent);
                else
                    Debug.WriteLine(message);
            }
            catch
            {
                // Logging must never destabilize path resolution; swallow after a debug write.
                Debug.WriteLine(message);
            }
        }

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
                normalizedPrimary = NormalizePath(primaryPath.Trim().Replace("\"", ""));
            }
            catch (Exception ex)
            {
                // Malformed path - fail closed by returning nothing extra; callers still use their
                // own primary path. We intentionally do not throw here, but do record why.
                LogSilent("SharedOutputFolder: could not normalize import path '" + primaryPath +
                    "': " + ex.Message);
                return paths;
            }

            paths.Add(normalizedPrimary);

            string sharedPath = ResolveSharedSibling(normalizedPrimary);
            if (sharedPath != null)
                paths.Add(sharedPath);

            return paths;
        }

        /// <summary>
        /// Given the files already selected from the primary folder (name -> byte length) and the
        /// candidate files discovered in the sibling shared folder (full path -> byte length),
        /// returns only the sibling files whose file name is NOT already present in the primary
        /// selection. Sibling files skipped because of a name collision are classified by size:
        ///   - <paramref name="skippedSameSize"/>: name AND size match - near-certain the same file.
        ///   - <paramref name="skippedDifferentSize"/>: name matches but size differs - AMBIGUOUS.
        ///     These are still skipped (primary wins, never auto double-import), but the caller should
        ///     warn more loudly so the user can decide whether to import the sibling copy manually.
        ///
        /// SQL LogScout places host/OS files in the shared folder OR the instance folder exclusively,
        /// so duplicates are not expected; when they do occur, the primary folder wins and the sibling
        /// copy is skipped to avoid importing the same data twice into the same tables (silent
        /// duplicate rows / overwrite of already-imported data). Size is used only to decide how
        /// loudly to warn - it is never used to auto-import a colliding name (that could double-import
        /// the same logical capture whose size drifted by a few bytes, e.g. a growing ERRORLOG).
        /// </summary>
        /// <param name="primaryNameToSize">
        /// File names (not full paths) already selected from the primary folder, mapped to their byte
        /// length. Comparison is case-insensitive. May be null/empty.
        /// </param>
        /// <param name="siblingFilesWithSize">
        /// Candidate sibling files as (full path -> byte length). A negative size means "unknown"
        /// (e.g. the length could not be read); such collisions are treated as different-size
        /// (ambiguous) so they are surfaced rather than quietly assumed identical.
        /// </param>
        /// <param name="skippedSameSize">Receives skipped sibling paths whose name+size matched. Never null.</param>
        /// <param name="skippedDifferentSize">Receives skipped sibling paths whose name matched but size differed. Never null.</param>
        /// <returns>The sibling full paths that are safe to import (no name collision with primary).</returns>
        public static List<string> FilterDuplicateSiblingFiles(
            IDictionary<string, long> primaryNameToSize,
            IEnumerable<KeyValuePair<string, long>> siblingFilesWithSize,
            out List<string> skippedSameSize,
            out List<string> skippedDifferentSize)
        {
            var accepted = new List<string>();
            skippedSameSize = new List<string>();
            skippedDifferentSize = new List<string>();

            if (siblingFilesWithSize == null)
                return accepted;

            var primary = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (primaryNameToSize != null)
            {
                foreach (KeyValuePair<string, long> kvp in primaryNameToSize)
                {
                    if (!string.IsNullOrEmpty(kvp.Key))
                        primary[kvp.Key] = kvp.Value;
                }
            }

            foreach (KeyValuePair<string, long> sibling in siblingFilesWithSize)
            {
                string full = sibling.Key;
                if (string.IsNullOrEmpty(full))
                    continue;

                string leaf = Path.GetFileName(full);
                long primarySize;
                if (!primary.TryGetValue(leaf, out primarySize))
                {
                    // No name collision - safe to import.
                    accepted.Add(full);
                    continue;
                }

                // Name collides: skip either way (primary wins). Classify by size for the warning.
                // A negative (unknown) size on either side is treated as "different" so it is not
                // quietly assumed to be identical.
                bool sameSize = primarySize >= 0 && sibling.Value >= 0 && primarySize == sibling.Value;
                if (sameSize)
                    skippedSameSize.Add(full);
                else
                    skippedDifferentSize.Add(full);
            }

            return accepted;
        }

        /// <summary>
        /// Returns the sibling files (full paths) that exist ONLY in the sibling shared folder - i.e.
        /// whose file name is NOT present in the primary folder. Used to warn about Custom XEL sources
        /// (SQLDiag / AlwaysOn_health / system_health) that live only in SharedOutputFiles and would
        /// therefore never be imported (the Custom XEL importer scans the primary folder only).
        ///
        /// A file that exists in BOTH folders is intentionally NOT returned: the primary copy is
        /// imported, so there is no data gap and telling the user to "move it to the primary folder"
        /// would be misleading (it is already there).
        /// </summary>
        /// <param name="primaryNames">
        /// File names (not full paths) present in the primary folder. Comparison is case-insensitive.
        /// May be null/empty.
        /// </param>
        /// <param name="siblingFiles">Full paths of candidate files found in the sibling folder.</param>
        /// <returns>Sibling full paths whose name is not present in the primary folder. Never null.</returns>
        public static List<string> GetSiblingOnlyFiles(
            ICollection<string> primaryNames,
            IEnumerable<string> siblingFiles)
        {
            var siblingOnly = new List<string>();
            if (siblingFiles == null)
                return siblingOnly;

            var primary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (primaryNames != null)
            {
                foreach (string name in primaryNames)
                {
                    if (!string.IsNullOrEmpty(name))
                        primary.Add(name);
                }
            }

            foreach (string full in siblingFiles)
            {
                if (string.IsNullOrEmpty(full))
                    continue;

                if (!primary.Contains(Path.GetFileName(full)))
                    siblingOnly.Add(full);
            }

            return siblingOnly;
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
            catch (Exception ex)
            {
                LogSilent("SharedOutputFolder: could not determine parent of '" + normalizedPrimary +
                    "': " + ex.Message);
                return null;
            }

            // No parent (e.g. a drive root) means there is no sibling location to look in.
            if (string.IsNullOrEmpty(parent))
                return null;

            string candidate;
            try
            {
                candidate = NormalizePath(Path.Combine(parent, SharedFolderName));
            }
            catch (Exception ex)
            {
                LogSilent("SharedOutputFolder: could not resolve sibling '" + SharedFolderName +
                    "' under '" + parent + "': " + ex.Message);
                return null;
            }

            // Do not treat the primary folder itself as its own shared sibling.
            if (string.Equals(candidate, normalizedPrimary, StringComparison.OrdinalIgnoreCase))
                return null;

            if (!Directory.Exists(candidate))
                return null;

            return candidate;
        }

        private static string NormalizePath(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string root = Path.GetPathRoot(fullPath);

            if (!string.IsNullOrEmpty(root) &&
                string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
            {
                // Preserve true drive/share roots (e.g. "C:\\") as rooted paths.
                return root;
            }

            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
