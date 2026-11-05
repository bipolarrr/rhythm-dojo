using System;
using System.Linq;
using RhythmDojo.Authoring;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class Pr12SceneCheck
{
    public static string Main()
    {
        var scene = EditorSceneManager.OpenScene(WorkspaceEditor.ScenePath);
        var view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<WorkspaceView>(true)).Single();
        if (!view.bpmSuggestion || !view.applyEstimatedBpm)
        {
            throw new Exception("Missing BPM suggestion reference");
        }
        if (view.actions.Single(binding => binding.action == WorkspaceAction.ApplyEstimatedBpm).button != view.applyEstimatedBpm)
        {
            throw new Exception("Apply action does not reference the suggestion button");
        }
        if (view.applyEstimatedBpm.gameObject.activeSelf || view.bpmSuggestion.text != "")
        {
            throw new Exception("Scene persisted a transient suggestion");
        }
        if (view.actions.Any(binding => !binding.button) || view.toolActions.Any(binding => !binding.button))
        {
            throw new Exception("Missing workspace action reference");
        }
        var timeline = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ChartAudioTimelineView>(true)).Single();
        timeline.ValidateReferences();
        var notice = view.menus[1].panel.GetComponentsInChildren<Text>(true)
            .Single(label => label.text == "임시, 자켓 정보만 받고 표시하는 엔드포인트가 없음.");
        if (notice.preferredWidth > notice.rectTransform.rect.width || notice.preferredHeight > notice.rectTransform.rect.height)
        {
            throw new Exception("Jacket notice clips");
        }
        return "Saved ChartEditor scene references, action binding, empty transient state, timeline references and jacket text bounds passed.";
    }
}
