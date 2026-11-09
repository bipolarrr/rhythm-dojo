using System;
using System.IO;

namespace RhythmDojo.Application
{
    public enum SongLoadFailureKind
    {
        MissingChart,
        InvalidChart,
        MissingAudio,
        AudioLoadFailed,
        Other,
    }

    public sealed class SongLoadFailure
    {
        public SongLoadFailureKind Kind { get; }
        public Exception Cause { get; }
        public string Message { get; }

        public SongLoadFailure(SongLoadFailureKind kind, string message, Exception cause = null)
        {
            Kind = kind;
            Message = message;
            Cause = cause;
        }

        public static SongLoadFailure FromException(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                throw exception;
            }

            if (exception is SongLoadException failure)
            {
                return failure.Failure;
            }

            return new SongLoadFailure(SongLoadFailureKind.Other, exception.Message, exception);
        }

        public static SongLoadFailure FromChartRead(Exception exception)
        {
            if (exception is FileNotFoundException || exception is DirectoryNotFoundException)
            {
                return new SongLoadFailure(SongLoadFailureKind.MissingChart, exception.Message, exception);
            }

            if (exception is ProjectValidationException)
            {
                return new SongLoadFailure(SongLoadFailureKind.InvalidChart, exception.Message, exception);
            }

            return FromChartValidation(exception);
        }

        public static SongLoadFailure FromChartValidation(Exception exception)
        {
            if (exception is ProjectValidationException validation)
            {
                var kind = SongLoadFailureKind.InvalidChart;
                if (validation.FieldPath == "song.audio")
                {
                    kind = SongLoadFailureKind.MissingAudio;
                }

                return new SongLoadFailure(kind, exception.Message, exception);
            }

            if (exception is ArgumentException || exception is FormatException
                || exception is InvalidOperationException)
            {
                return new SongLoadFailure(SongLoadFailureKind.InvalidChart, exception.Message, exception);
            }

            return FromException(exception);
        }

        public static SongLoadFailure FromAudio(Exception exception)
        {
            if (exception is OperationCanceledException || exception is SongLoadException)
            {
                return FromException(exception);
            }

            var kind = SongLoadFailureKind.AudioLoadFailed;
            if (exception is FileNotFoundException || exception is DirectoryNotFoundException)
            {
                kind = SongLoadFailureKind.MissingAudio;
            }

            return new SongLoadFailure(kind, exception.Message, exception);
        }
    }
}
