namespace TraceEventImporter.Normalization
{
    /// <summary>
    /// Exact port of StringHash() from READ80TRACE/Analyze.cpp.
    /// Produces a 64-bit hash from normalized SQL text using dual-hash
    /// (One-At-A-Time + Bernstein's) with periodic DWORD swaps.
    /// </summary>
    public static class HashComputer
    {
        /// <summary>
        /// Compute a 64-bit hash of the normalized text.
        /// Must produce identical results to the C++ StringHash() function.
        /// </summary>
        /// <param name="normalizedText">Uppercased, whitespace-collapsed, placeholder-substituted SQL text</param>
        /// <param name="specialProcId">Special procedure ID (0 for regular batches, nonzero for sp_executesql etc.)</param>
        /// <returns>64-bit hash as a signed long (matching SQL bigint storage)</returns>
        public static long ComputeHash(string normalizedText, int specialProcId = 0)
        {
            if (string.IsNullOrEmpty(normalizedText))
                return 0;

            int charCount = normalizedText.Length;

            // Seed: dwHighHash = charCount, dwLowHash = specialProcId
            // In the C++ struct, dwHighHash is at offset 0 (low 32 bits of uint64),
            // dwLowHash is at offset 4 (high 32 bits of uint64) on little-endian.
            uint dwHighHash = (uint)charCount;
            uint dwLowHash = (uint)specialProcId;

            for (int i = 0; i < charCount; i++)
            {
                ushort c = normalizedText[i];

                // One At A Time hash on dwLowHash
                dwLowHash += c;
                dwLowHash += (dwLowHash << 10);
                dwLowHash ^= (dwLowHash >> 6);

                // Bernstein's hash on dwHighHash: ((self << 5) + self) == *33
                dwHighHash = ((dwHighHash << 5) + dwHighHash) + c;

                // Swap DWORD halves to avoid funnel patterns.
                // Check next char for '!' (33) or current position divisible by 32.
                // When i is the last char, "next char" is the null terminator (0) in C++,
                // which is != 33, so only the modulo check applies.
                bool shouldSwap = (i % 32 == 0);
                if (!shouldSwap && i + 1 < charCount)
                {
                    shouldSwap = (normalizedText[i + 1] == '!');
                }

                if (shouldSwap)
                {
                    uint temp = dwLowHash;
                    dwLowHash = dwHighHash;
                    dwHighHash = temp;
                }
            }

            // Final One At A Time finalization on dwLowHash
            dwLowHash += (dwLowHash << 3);
            dwLowHash ^= (dwLowHash >> 11);
            dwLowHash += (dwLowHash << 15);

            // Combine into 64-bit value matching C++ union layout on little-endian:
            // dwHighHash at offset 0 = low 32 bits, dwLowHash at offset 4 = high 32 bits
            ulong result = ((ulong)dwLowHash << 32) | dwHighHash;
            return unchecked((long)result);
        }
    }
}
