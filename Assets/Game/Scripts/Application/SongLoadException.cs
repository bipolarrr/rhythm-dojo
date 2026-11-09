using System;

namespace RhythmDojo.Application
{
    public sealed class SongLoadException : Exception
    {
        public SongLoadFailure Failure { get; }

        public SongLoadException(SongLoadFailure failure)
            : base(failure.Message, failure.Cause)
        {
            Failure = failure;
        }
    }
}
