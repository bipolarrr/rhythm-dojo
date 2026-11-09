using System;
using RhythmDojo.Application;

namespace RhythmDojo.UI
{
    public sealed class SongLoadErrorDisplay
    {
        public string Message { get; }
        public bool CanRetry { get; }

        public SongLoadErrorDisplay(SongLoadFailure failure)
        {
            switch (failure?.Kind ?? SongLoadFailureKind.Other)
            {
                case SongLoadFailureKind.MissingChart:
                    Message = "채보 파일을 찾을 수 없어요.\n파일을 복구한 뒤 곡 목록을 새로고침해 주세요.";
                    break;
                case SongLoadFailureKind.InvalidChart:
                    Message = "채보를 재생할 수 없어요.\n에디터에서 수정한 뒤 곡 목록을 새로고침해 주세요.";
                    break;
                case SongLoadFailureKind.MissingAudio:
                    Message = "음원 파일을 찾을 수 없어요.\n음원을 다시 연결한 뒤 곡 목록을 새로고침해 주세요.";
                    break;
                case SongLoadFailureKind.AudioLoadFailed:
                    Message = "음원을 불러오지 못했어요.\n다시 시도해 주세요.";
                    CanRetry = true;
                    break;
                default:
                    Message = "곡을 불러오지 못했어요.\n다시 시도해 주세요.";
                    CanRetry = true;
                    break;
            }
        }

        public static SongLoadErrorDisplay FromException(Exception exception) =>
            new SongLoadErrorDisplay(SongLoadFailure.FromException(exception));
    }
}
