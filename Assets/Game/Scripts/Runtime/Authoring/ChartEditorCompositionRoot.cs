using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Content;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace RhythmDojo.Authoring
{
    public sealed class ChartEditorCompositionRoot : MonoBehaviour
    {
        [SerializeField] private GameSettings settings;
        [SerializeField] private ChartEditorScreen screen;
        [SerializeField] private ChartEditorMenu menu;

        private void Start()
        {
            var store = new FileSongProjectStore(Path.Combine(UnityEngine.Application.persistentDataPath, "Songs"));
            var flow = AppFlowController.Create(settings);
            menu.Initialize(store, flow, project => new DocumentSongLoader(
                id => id == screen.Mode.name ? screen.Mode : null,
                (audio, token) => LoadAudio(store.ResolveAudioPath(project.Id, audio), token)));
        }

        private static async Task<SongAudioLease> LoadAudio(string path, CancellationToken token)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            var type = extension == ".wav" ? AudioType.WAV : extension == ".ogg" ? AudioType.OGGVORBIS : AudioType.MPEG;
            using var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type);
            var operation = request.SendWebRequest();
            while (!operation.isDone) { token.ThrowIfCancellationRequested(); await Task.Yield(); }
            token.ThrowIfCancellationRequested();
            if (request.result != UnityWebRequest.Result.Success) throw new IOException("음원을 읽을 수 없습니다: " + request.error);
            var clip = DownloadHandlerAudioClip.GetContent(request);
            if (!clip) throw new IOException("음원 데이터가 비어 있습니다.");
            return new SongAudioLease(clip, () => UnityEngine.Object.Destroy(clip));
        }
    }
}
