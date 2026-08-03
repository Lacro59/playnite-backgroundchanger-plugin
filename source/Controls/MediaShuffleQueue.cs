using System;
using System.Collections.Generic;
using System.Text;

namespace BackgroundChanger.Controls
{
    /// <summary>
    /// In-memory shuffled index queue for Auto Changer random order.
    /// Guarantees each index in <c>[0, poolSize)</c> appears once per cycle (Fisher-Yates),
    /// then reshuffles. State is session-only; each media control should hold its own instance
    /// (media kind is implicit per control).
    /// </summary>
    public sealed class MediaShuffleQueue
    {
        private readonly Random _random = new Random();

        private Guid _boundGameId;
        private int _boundPoolSize;
        private string _boundFingerprint;
        private int[] _order;
        private int _cursor;
        private int _lastIndex = -1;

        /// <summary>
        /// Gets the 1-based position in the current cycle after the last <see cref="Next"/> call,
        /// or 0 when the queue has no active cycle.
        /// </summary>
        public int CycleIndex { get; private set; }

        /// <summary>
        /// Gets the pool size of the current cycle after the last <see cref="Next"/> call,
        /// or 0 when the queue has no active cycle.
        /// </summary>
        public int CycleTotal { get; private set; }

        /// <summary>
        /// Builds a stable fingerprint from ordered full paths for pool-change detection.
        /// </summary>
        /// <param name="fullPaths">Full paths in display order; null entries are skipped.</param>
        /// <returns>Joined fingerprint, or an empty string when there are no paths.</returns>
        public static string BuildFingerprint(IEnumerable<string> fullPaths)
        {
            if (fullPaths == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            foreach (string path in fullPaths)
            {
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(path);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Returns the next shuffled index for the given game pool.
        /// Regenerates when the queue is exhausted or the pool identity changes
        /// (<paramref name="gameId"/>, <paramref name="poolSize"/>, or <paramref name="poolFingerprint"/>).
        /// </summary>
        /// <param name="gameId">Game identity bound to this queue.</param>
        /// <param name="poolSize">Current item count (N).</param>
        /// <param name="poolFingerprint">
        /// Optional stable fingerprint of pool contents (see <see cref="BuildFingerprint"/>).
        /// When null or empty, only game id and size trigger invalidation.
        /// </param>
        /// <returns>Index in range <c>[0, poolSize)</c>, or -1 when <paramref name="poolSize"/> is &lt;= 0.</returns>
        public int Next(Guid gameId, int poolSize, string poolFingerprint = null)
        {
            if (poolSize <= 0)
            {
                ClearState();
                return -1;
            }

            string fingerprint = poolFingerprint ?? string.Empty;

            if (poolSize == 1)
            {
                Bind(gameId, 1, fingerprint);
                _order = new[] { 0 };
                _cursor = 1;
                CycleIndex = 1;
                CycleTotal = 1;
                _lastIndex = 0;
                return 0;
            }

            if (!IsBoundTo(gameId, poolSize, fingerprint) || _order == null || _cursor >= _order.Length)
            {
                Reshuffle(gameId, poolSize, fingerprint);
            }

            int index = _order[_cursor];
            _cursor++;
            CycleIndex = _cursor;
            CycleTotal = _order.Length;
            _lastIndex = index;
            return index;
        }

        /// <summary>
        /// Clears bound state so the next <see cref="Next"/> call reshuffles from scratch.
        /// </summary>
        public void Reset()
        {
            ClearState();
        }

        /// <summary>
        /// Records the index currently on screen so the next reshuffle avoids an immediate repeat
        /// (e.g. stable timer entry before the first auto-changer tick).
        /// </summary>
        /// <param name="index">Displayed pool index, or a negative value to clear.</param>
        public void RememberDisplayedIndex(int index)
        {
            _lastIndex = index;
        }

        private bool IsBoundTo(Guid gameId, int poolSize, string fingerprint)
        {
            if (_boundGameId != gameId || _boundPoolSize != poolSize)
            {
                return false;
            }

            if (string.IsNullOrEmpty(_boundFingerprint) && string.IsNullOrEmpty(fingerprint))
            {
                return true;
            }

            return string.Equals(_boundFingerprint, fingerprint, StringComparison.Ordinal);
        }

        private void Reshuffle(Guid gameId, int poolSize, string fingerprint)
        {
            int previousLast = _lastIndex;
            Bind(gameId, poolSize, fingerprint);

            int[] order = new int[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                order[i] = i;
            }

            // Fisher-Yates
            for (int i = poolSize - 1; i > 0; i--)
            {
                int j = _random.Next(0, i + 1);
                int tmp = order[i];
                order[i] = order[j];
                order[j] = tmp;
            }

            // Avoid immediate repeat across cycle boundary when possible
            if (previousLast >= 0 && poolSize > 1 && order[0] == previousLast)
            {
                int swapWith = _random.Next(1, poolSize);
                int tmp = order[0];
                order[0] = order[swapWith];
                order[swapWith] = tmp;
            }

            _order = order;
            _cursor = 0;
            CycleIndex = 0;
            CycleTotal = poolSize;
        }

        private void Bind(Guid gameId, int poolSize, string fingerprint)
        {
            _boundGameId = gameId;
            _boundPoolSize = poolSize;
            _boundFingerprint = fingerprint ?? string.Empty;
        }

        private void ClearState()
        {
            _boundGameId = Guid.Empty;
            _boundPoolSize = 0;
            _boundFingerprint = null;
            _order = null;
            _cursor = 0;
            _lastIndex = -1;
            CycleIndex = 0;
            CycleTotal = 0;
        }
    }
}
