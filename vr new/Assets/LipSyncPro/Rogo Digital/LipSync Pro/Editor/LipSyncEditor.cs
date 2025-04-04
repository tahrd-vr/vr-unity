﻿using RogoDigital;
using RogoDigital.Lipsync;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.Animations;
using UnityEngine;

[CustomEditor(typeof(LipSync))]
public class LipSyncEditor : Editor
{
#pragma warning disable 618

	private LipSync lsTarget;

	private string[] blendables;
	private SerializedObject serializedTarget;
	private int markerTab = 0;
	private bool saving = false;
	private string savingName = "";

	private AnimBool showBoneOptions;
	private AnimBool showPlayOnAwake;
	private AnimBool showFixedFrameRate;

	private LipSyncProject settings;
	private AnimatorController controller;
	private bool regenGestures = false;

	private int blendSystemNumber = 0;

	private Texture2D logo;

	private Texture2D presetsIcon;
	private Texture2D warningIcon;

	private GUIStyle miniLabelDark;

	private SerializedProperty audioSource;
	private SerializedProperty restTime;
	private SerializedProperty restHoldTime;
	private SerializedProperty phonemeCurveGenerationMode;
	private SerializedProperty emotionCurveGenerationMode;
	private SerializedProperty playOnAwake;
	private SerializedProperty loop;
	private SerializedProperty defaultClip;
	private SerializedProperty defaultDelay;
	private SerializedProperty scaleAudioSpeed;
	private SerializedProperty animationTimingMode;
	private SerializedProperty frameRate;
	private SerializedProperty useBones;
	private SerializedProperty boneUpdateAnimation;
	private SerializedProperty onFinishedPlaying;
	private SerializedProperty gesturesAnimator;

	private float versionNumber = 1.43f;

	void OnEnable ()
	{
		logo = (Texture2D)EditorGUIUtility.Load("Rogo Digital/Lipsync/Dark/logo_component.png");

		presetsIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Dark/presets.png");
		warningIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/emotion-error.png");

		if (!EditorGUIUtility.isProSkin)
		{
			logo = (Texture2D)EditorGUIUtility.Load("Rogo Digital/Lipsync/Light/logo_component.png");
			presetsIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Light/presets.png");
		}

		Undo.undoRedoPerformed += OnUndoRedoPerformed;

		lsTarget = (LipSync)target;
		lsTarget.reset += OnEnable;

		if (lsTarget.lastUsedVersion < versionNumber)
		{
			AutoUpdate(lsTarget.lastUsedVersion);
			lsTarget.lastUsedVersion = versionNumber;
		}

		if (lsTarget.gesturesAnimator != null)
		{
			string assetPath = AssetDatabase.GetAssetPath(lsTarget.gesturesAnimator.runtimeAnimatorController);
			controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
		}

		serializedTarget = new SerializedObject(target);
		blendSystemNumber = BlendSystemEditor.FindBlendSystems(lsTarget);

		audioSource = serializedTarget.FindProperty("audioSource");
		restTime = serializedTarget.FindProperty("restTime");
		restHoldTime = serializedTarget.FindProperty("restHoldTime");
		phonemeCurveGenerationMode = serializedTarget.FindProperty("phonemeCurveGenerationMode");
		emotionCurveGenerationMode = serializedTarget.FindProperty("emotionCurveGenerationMode");
		playOnAwake = serializedTarget.FindProperty("playOnAwake");
		loop = serializedTarget.FindProperty("loop");
		defaultClip = serializedTarget.FindProperty("defaultClip");
		defaultDelay = serializedTarget.FindProperty("defaultDelay");
		scaleAudioSpeed = serializedTarget.FindProperty("scaleAudioSpeed");
		animationTimingMode = serializedTarget.FindProperty("m_animationTimingMode");
		frameRate = serializedTarget.FindProperty("frameRate");
		useBones = serializedTarget.FindProperty("useBones");
		boneUpdateAnimation = serializedTarget.FindProperty("boneUpdateAnimation");
		onFinishedPlaying = serializedTarget.FindProperty("onFinishedPlaying");
		gesturesAnimator = serializedTarget.FindProperty("gesturesAnimator");

		showBoneOptions = new AnimBool(lsTarget.useBones, Repaint);
		showPlayOnAwake = new AnimBool(lsTarget.playOnAwake, Repaint);
		showFixedFrameRate = new AnimBool(lsTarget.animationTimingMode == LipSync.AnimationTimingMode.FixedFrameRate, Repaint);

		if (lsTarget.blendSystem != null)
		{
			if (lsTarget.blendSystem.isReady)
			{
				GetBlendShapes();
				lsTarget.blendSystem.onBlendablesChanged += GetBlendShapes;
				BlendSystemEditor.GetBlendSystemButtons(lsTarget.blendSystem);
			}
		}

		//Get Settings File
		string[] guids = AssetDatabase.FindAssets("ProjectSettings t:LipSyncProject");
		string path = "";

		if (guids.Length > 0)
		{
			path = AssetDatabase.GUIDToAssetPath(guids[0]);

			if (guids.Length > 1)
				Debug.LogWarning("LipSync: Multiple LipSyncProject files found. Only one will be used.");
		}

		settings = (LipSyncProject)AssetDatabase.LoadAssetAtPath(path, typeof(LipSyncProject));

		if (settings == null)
		{
			settings = ScriptableObject.CreateInstance<LipSyncProject>();

			LipSyncProject newSettings = ScriptableObject.CreateInstance<LipSyncProject>();
			newSettings.emotions = new string[] { "default" };
			newSettings.emotionColors = new Color[] { new Color(1f, 0.7f, 0.1f) };

			EditorUtility.CopySerialized(newSettings, settings);
			AssetDatabase.CreateAsset(settings, "Assets/Rogo Digital/LipSync/ProjectSettings.asset");
			AssetDatabase.Refresh();
			DestroyImmediate(newSettings);
		}

		if (lsTarget.gestures == null)
		{
			lsTarget.gestures = new List<GestureInstance>();
		}

		if (controller != null)
		{
			// Sync gestures
			for (int a = 0; a < settings.gestures.Count; a++)
			{
				bool found = false;
				for (int b = 0; b < lsTarget.gestures.Count; b++)
				{
					if (lsTarget.gestures[b].gesture == settings.gestures[a] && a == b)
					{
						found = true;
						break;
					}
					else if (lsTarget.gestures[b].gesture == settings.gestures[a])
					{
						lsTarget.gestures.Insert(a, lsTarget.gestures[b]);
						int newIndex = a > b ? b : b + 1;
						lsTarget.gestures.RemoveAt(newIndex);
						found = true;
						break;
					}
				}

				if (!found)
				{
					lsTarget.gestures.Insert(a, new GestureInstance(settings.gestures[a], null, ""));
				}
			}

			for (int a = lsTarget.gestures.Count - 1; a >= 0; a--)
			{
				if (!settings.gestures.Contains(lsTarget.gestures[a].gesture))
				{
					lsTarget.gestures.Remove(lsTarget.gestures[a]);
				}
				else if (string.IsNullOrEmpty(lsTarget.gestures[a].triggerName))
				{
					regenGestures = true;
				}
			}
		}

		// Mark phonemes as invalid
		for (int a = 0; a < lsTarget.phonemes.Count; a++)
		{
			lsTarget.phonemes[a].verified = false;
		}

		// Get phonemes from PhonemeSet
		if (settings.phonemeSet != null)
		{
			for (int a = 0; a < settings.phonemeSet.phonemes.Length; a++)
			{
				bool wasFound = false;
				for (int b = 0; b < lsTarget.phonemes.Count; b++)
				{
					// Verify existing phoneme
					if (lsTarget.phonemes[b].phonemeName == settings.phonemeSet.phonemes[a].name)
					{
						lsTarget.phonemes[b].verified = wasFound = true;
						break;
					}
				}

				if (!wasFound)
				{
					// Add new ones
					lsTarget.phonemes.Add(new PhonemeShape(settings.phonemeSet.phonemes[a].name));
				}
			}
		}

		if (lsTarget.emotions == null)
		{
			lsTarget.emotions = new List<EmotionShape>();

			for (int a = 0; a < settings.emotions.Length; a++)
			{
				lsTarget.emotions.Add(new EmotionShape(settings.emotions[a]));
			}

			EditorUtility.SetDirty(lsTarget);
			serializedTarget.SetIsDifferentCacheDirty();
		}
		else
		{
			foreach (EmotionShape eShape in lsTarget.emotions)
			{
				bool exists = false;
				foreach (string emotion in settings.emotions)
				{
					if (eShape.emotion == emotion)
					{
						exists = true;
						eShape.verified = true;
					}
				}

				if (!exists)
				{
					eShape.verified = false;
				}
			}

			foreach (string emotion in settings.emotions)
			{
				bool exists = false;
				foreach (EmotionShape eShape in lsTarget.emotions)
				{
					if (eShape.emotion == emotion)
					{
						exists = true;
					}
				}

				if (!exists)
				{
					lsTarget.emotions.Add(new EmotionShape(emotion));

					EditorUtility.SetDirty(lsTarget);
					serializedTarget.SetIsDifferentCacheDirty();
				}
			}
		}
	}

	void OnDisable ()
	{
		Undo.undoRedoPerformed -= OnUndoRedoPerformed;
		lsTarget.reset -= OnEnable;

		if (lsTarget.blendSystem != null)
		{
			if (lsTarget.blendSystem.isReady)
			{
				lsTarget.blendSystem.onBlendablesChanged -= GetBlendShapes;
				foreach (Shape shape in lsTarget.phonemes)
				{
					for (int blendable = 0; blendable < shape.weights.Count; blendable++)
					{
						lsTarget.blendSystem.SetBlendableValue(shape.blendShapes[blendable], 0);
					}
				}
				foreach (Shape shape in lsTarget.emotions)
				{
					for (int blendable = 0; blendable < shape.weights.Count; blendable++)
					{
						lsTarget.blendSystem.SetBlendableValue(shape.blendShapes[blendable], 0);
					}
				}
			}
		}

		if (LipSyncEditorExtensions.currentToggle > -1 && lsTarget.useBones)
		{
			foreach (Shape shape in lsTarget.phonemes)
			{
				foreach (BoneShape bone in shape.bones)
				{
					if (bone.bone != null)
					{
						bone.bone.localPosition = bone.neutralPosition;
						bone.bone.localEulerAngles = bone.neutralRotation;
						bone.bone.localScale = bone.neutralScale;
					}
				}
			}
		}

		LipSyncEditorExtensions.currentToggle = -1;
	}

	void OnUndoRedoPerformed ()
	{
		if (LipSyncEditorExtensions.oldToggle > -1 && lsTarget.useBones)
		{
			if (markerTab == 0)
			{
				foreach (BoneShape boneshape in lsTarget.phonemes[LipSyncEditorExtensions.oldToggle].bones)
				{
					if (boneshape.bone != null)
					{
						boneshape.bone.localPosition = boneshape.neutralPosition;
						boneshape.bone.localEulerAngles = boneshape.neutralRotation;
						boneshape.bone.localScale = boneshape.neutralScale;
					}
				}
			}
			else if (markerTab == 1)
			{
				foreach (BoneShape boneshape in lsTarget.emotions[LipSyncEditorExtensions.oldToggle].bones)
				{
					if (boneshape.bone != null)
					{
						boneshape.bone.localPosition = boneshape.neutralPosition;
						boneshape.bone.localEulerAngles = boneshape.neutralRotation;
						boneshape.bone.localScale = boneshape.neutralScale;
					}
				}
			}
		}

		if (markerTab == 0)
		{
			foreach (PhonemeShape shape in lsTarget.phonemes)
			{
				foreach (int blendable in shape.blendShapes)
				{
					lsTarget.blendSystem.SetBlendableValue(blendable, 0);
				}
			}
		}
		else if (markerTab == 1)
		{
			foreach (EmotionShape shape in lsTarget.emotions)
			{
				foreach (int blendable in shape.blendShapes)
				{
					lsTarget.blendSystem.SetBlendableValue(blendable, 0);
				}
			}
		}

		if (LipSyncEditorExtensions.currentToggle > -1)
		{
			if (markerTab == 0)
			{
				for (int b = 0; b < lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].blendShapes.Count; b++)
				{
					lsTarget.blendSystem.SetBlendableValue(lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].blendShapes[b], lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].weights[b]);
				}
			}
			else if (markerTab == 1)
			{
				for (int b = 0; b < lsTarget.emotions[LipSyncEditorExtensions.currentToggle].blendShapes.Count; b++)
				{
					lsTarget.blendSystem.SetBlendableValue(lsTarget.emotions[LipSyncEditorExtensions.currentToggle].blendShapes[b], lsTarget.emotions[LipSyncEditorExtensions.currentToggle].weights[b]);
				}
			}
		}

		blendSystemNumber = BlendSystemEditor.FindBlendSystems(lsTarget);
	}

	public override void OnInspectorGUI ()
	{
		if (serializedTarget == null)
		{
			OnEnable();
		}

		if (miniLabelDark == null)
		{
			miniLabelDark = new GUIStyle(EditorStyles.miniLabel);
			miniLabelDark.normal.textColor = Color.black;
		}

		serializedTarget.Update();

		EditorGUI.BeginDisabledGroup(saving);
		Rect fullheight = EditorGUILayout.BeginVertical();

		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		GUILayout.Box(logo, GUIStyle.none);
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
		EditorGUI.BeginChangeCheck();
		blendSystemNumber = BlendSystemEditor.DrawBlendSystemEditor(lsTarget, blendSystemNumber, "LipSync Pro requires a blend system to function.");

		if (lsTarget.blendSystem != null)
		{
			if (lsTarget.blendSystem.isReady)
			{

				if (blendables == null)
				{
					lsTarget.blendSystem.onBlendablesChanged += GetBlendShapes;
					GetBlendShapes();
				}

				EditorGUILayout.Space();
				EditorGUILayout.PropertyField(audioSource, new GUIContent("Audio Source", "AudioSource to play dialogue from."));

				EditorGUILayout.Space();
				EditorGUILayout.PropertyField(useBones, new GUIContent("Use Bone Transforms", "Allow BoneShapes to be added to phoneme poses. This enables the use of bone based facial animation."));
				showBoneOptions.target = lsTarget.useBones;
				if (EditorGUILayout.BeginFadeGroup(showBoneOptions.faded))
				{
					EditorGUILayout.PropertyField(boneUpdateAnimation, new GUIContent("Account for Animation", "If true, will calculate relative bone positions/rotations each frame. Improves results when using animation, but will cause errors when not."));
					EditorGUILayout.Space();
				}
				FixedEndFadeGroup(showBoneOptions.faded);
				EditorGUILayout.Space();
				BlendSystemEditor.DrawBlendSystemButtons(lsTarget.blendSystem);
				int oldTab = markerTab;

				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				markerTab = GUILayout.Toolbar(markerTab, new GUIContent[] { new GUIContent("Phonemes"), new GUIContent("Emotions"), new GUIContent("Gestures", regenGestures ? warningIcon : null) }, GUILayout.MaxWidth(400), GUILayout.MinHeight(23));

				Rect presetRect = EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button(new GUIContent(presetsIcon, "Presets"), GUILayout.MaxWidth(32), GUILayout.MinHeight(23)))
				{
					GenericMenu menu = new GenericMenu();

					string[] directories = Directory.GetDirectories(Application.dataPath, "Presets", SearchOption.AllDirectories);

					bool noItems = true;
					foreach (string directory in directories)
					{
						foreach (string file in Directory.GetFiles(directory))
						{
							if (Path.GetExtension(file).ToLower() == ".asset")
							{
								LipSyncPreset preset = AssetDatabase.LoadAssetAtPath<LipSyncPreset>("Assets" + file.Substring((Application.dataPath).Length));
								if (preset != null)
								{
									noItems = false;
									menu.AddItem(new GUIContent(Path.GetFileNameWithoutExtension(file)), false, LoadPreset, file);
								}
							}
						}

						string[] subdirectories = Directory.GetDirectories(directory);
						foreach (string subdirectory in subdirectories)
						{
							foreach (string file in Directory.GetFiles(subdirectory))
							{
								if (Path.GetExtension(file).ToLower() == ".asset")
								{
									LipSyncPreset preset = AssetDatabase.LoadAssetAtPath<LipSyncPreset>("Assets" + file.Substring((Application.dataPath).Length));
									if (preset != null)
									{
										noItems = false;
										menu.AddItem(new GUIContent(Path.GetFileName(subdirectory) + "/" + Path.GetFileNameWithoutExtension(file)), false, LoadPreset, file);
									}
								}
							}
						}
					}

					if (noItems)
						menu.AddDisabledItem(new GUIContent("No Presets Found"));

					menu.AddSeparator("");
					menu.AddItem(new GUIContent("Save New Preset"), false, NewPreset);
					if (AssetDatabase.FindAssets("t:BlendShapePreset").Length > 0)
					{
						menu.AddDisabledItem(new GUIContent("Old-style presets found. Convert them to use."));
					}

					menu.DropDown(presetRect);
				}
				EditorGUILayout.EndHorizontal();

				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				GUILayout.Space(10);

				if (markerTab != oldTab)
				{
					if (oldTab == 0)
					{
						foreach (PhonemeShape phoneme in lsTarget.phonemes)
						{
							foreach (int shape in phoneme.blendShapes)
							{
								lsTarget.blendSystem.SetBlendableValue(shape, 0);
							}
						}
					}
					else
					{
						foreach (EmotionShape emotion in lsTarget.emotions)
						{
							foreach (int shape in emotion.blendShapes)
							{
								lsTarget.blendSystem.SetBlendableValue(shape, 0);
							}
						}
					}
					if (LipSyncEditorExtensions.currentTarget == lsTarget)
						LipSyncEditorExtensions.currentToggle = -1;
				}

				if (markerTab == 0)
				{
					int a = 0;
					foreach (PhonemeShape phoneme in lsTarget.phonemes)
					{
						if (this.DrawShapeEditor(lsTarget.blendSystem, blendables, lsTarget.useBones, true, phoneme, phoneme.phonemeName + " Phoneme", a, "Phoneme does not exist in the chosen Phoneme Set. You can change Phoneme Sets from the Project Settings."))
						{
							// Delete Phoneme
							Undo.RecordObject(lsTarget, "Delete Phoneme");
							foreach (int blendable in phoneme.blendShapes)
							{
								lsTarget.blendSystem.SetBlendableValue(blendable, 0);
							}

							lsTarget.phonemes.Remove(phoneme);
							LipSyncEditorExtensions.currentToggle = -1;
							LipSyncEditorExtensions.selectedBone = 0;
							EditorUtility.SetDirty(lsTarget.gameObject);
							serializedTarget.SetIsDifferentCacheDirty();
							break;
						}
						a++;
					}
				}
				else if (markerTab == 1)
				{
					int a = 0;
					foreach (EmotionShape emotion in lsTarget.emotions)
					{
						if (emotion.blendShapes == null)
							emotion.blendShapes = new List<int>();
						if (this.DrawShapeEditor(lsTarget.blendSystem, blendables, lsTarget.useBones, true, emotion, emotion.emotion + " Emotion", a))
						{

							// Delete Emotion
							Undo.RecordObject(lsTarget, "Delete Emotion");
							foreach (int blendable in emotion.blendShapes)
							{
								lsTarget.blendSystem.SetBlendableValue(blendable, 0);
							}

							lsTarget.emotions.Remove(emotion);
							LipSyncEditorExtensions.currentToggle = -1;
							LipSyncEditorExtensions.selectedBone = 0;
							EditorUtility.SetDirty(lsTarget.gameObject);
							serializedTarget.SetIsDifferentCacheDirty();
							break;
						}
						a++;
					}
					EditorGUILayout.Space();

					EditorGUILayout.BeginHorizontal();
					GUILayout.FlexibleSpace();
					if (GUILayout.Button("Edit Emotions", GUILayout.MaxWidth(300), GUILayout.Height(25)))
					{
						LipSyncProjectSettings.ShowWindow();
					}
					GUILayout.FlexibleSpace();
					EditorGUILayout.EndHorizontal();
				}
				else if (markerTab == 2)
				{
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField(gesturesAnimator, new GUIContent("Animator", "The animator component used for playing Gestures."));
					if (EditorGUI.EndChangeCheck())
					{
						if (lsTarget.gesturesAnimator != null)
						{
							string assetPath = AssetDatabase.GetAssetPath(lsTarget.gesturesAnimator.runtimeAnimatorController);
							controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
						}
					}
					EditorGUILayout.Space();

					if (lsTarget.gesturesAnimator != null)
					{
						if (lsTarget.gesturesAnimator.runtimeAnimatorController == null)
						{
							controller = null;
						}
						else if (controller == null)
						{
							string assetPath = AssetDatabase.GetAssetPath(lsTarget.gesturesAnimator.runtimeAnimatorController);
							controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
						}

						if (controller != null)
						{
							if (lsTarget.gestures.Count > 0)
							{
								EditorGUILayout.LabelField("Gestures", EditorStyles.boldLabel);
								bool allAssigned = true;
								for (int a = 0; a < lsTarget.gestures.Count; a++)
								{
									Rect lineRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(25));
									if (a % 2 == 0)
									{
										GUI.Box(lineRect, "", (GUIStyle)"hostview");
									}
									GUILayout.Space(4);
									EditorGUILayout.LabelField(lsTarget.gestures[a].gesture);
									GUILayout.FlexibleSpace();
									lsTarget.gestures[a].clip = (AnimationClip)EditorGUILayout.ObjectField(lsTarget.gestures[a].clip, typeof(AnimationClip), false);
									if (lsTarget.gestures[a].clip == null)
										allAssigned = false;
									EditorGUILayout.EndHorizontal();
								}
								GUILayout.Space(10);
								EditorGUILayout.BeginHorizontal();
								GUILayout.FlexibleSpace();
								if (GUILayout.Button("Begin Setup", GUILayout.MaxWidth(200), GUILayout.Height(20)))
								{
									GestureSetupWizard.ShowWindow(lsTarget, controller);
								}
								GUILayout.FlexibleSpace();
								EditorGUILayout.EndHorizontal();
								GUILayout.Space(10);
								if (!allAssigned)
								{
									EditorGUILayout.HelpBox("Not all Gestures have AnimationClips assigned. These gestures will have no effect on this character.", MessageType.Warning);
								}
							}
							else
							{
								EditorGUILayout.HelpBox("There are no Gestures defined in the project settings.", MessageType.Info);
							}
						}
						else
						{
							EditorGUILayout.HelpBox("Chosen Animator does not have an AnimatorController assigned.", MessageType.Error);
						}
					}
					else
					{
						EditorGUILayout.HelpBox("Select an Animator component to enable gesture support.", MessageType.Warning);
					}

					if (regenGestures)
					{
						EditorGUILayout.HelpBox("Gestures need regenerating - run the Gesture Setup Wizard.", MessageType.Warning);
					}

					EditorGUILayout.Space();
					EditorGUILayout.BeginHorizontal();
					GUILayout.FlexibleSpace();
					if (GUILayout.Button("Edit Gestures", GUILayout.MaxWidth(300), GUILayout.Height(25)))
					{
						LipSyncProjectSettings.ShowWindow();
					}
					GUILayout.FlexibleSpace();
					EditorGUILayout.EndHorizontal();
				}

				EditorGUILayout.Space();
				GUILayout.Box("General Animation Settings", EditorStyles.boldLabel);
				if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
				{
					if (lsTarget.animationTimingMode == LipSync.AnimationTimingMode.AudioPlayback)
					{
						animationTimingMode.enumValueIndex = 1;
					}
					EditorGUILayout.HelpBox("AudioPlayback timing mode is incompatible with WebGL target.", MessageType.Info);
					animationTimingMode.enumValueIndex = EditorGUILayout.IntPopup("Timing Mode", animationTimingMode.enumValueIndex, new string[] { "CustomTimer", "FixedFrameRate" }, new int[] { 1, 2 });
				}
				else
				{
					EditorGUILayout.PropertyField(animationTimingMode, new GUIContent("Timing Mode", "How animations are sampled: AudioPlayback uses the audioclip, CustomTimer uses a framerate independent timer, FixedFrameRate is framerate dependent."));
				}
				showFixedFrameRate.target = lsTarget.animationTimingMode == LipSync.AnimationTimingMode.FixedFrameRate;
				if (EditorGUILayout.BeginFadeGroup(showFixedFrameRate.faded))
				{
					EditorGUILayout.PropertyField(frameRate, new GUIContent("Frame Rate", "The framerate to play the animation at."));
				}
				EditorGUILayout.EndFadeGroup();
				EditorGUILayout.PropertyField(playOnAwake, new GUIContent("Play On Awake", "If checked, the default clip will play when the script awakes."));
				showPlayOnAwake.target = lsTarget.playOnAwake;
				if (EditorGUILayout.BeginFadeGroup(showPlayOnAwake.faded))
				{
					EditorGUILayout.PropertyField(defaultClip, new GUIContent("Default Clip", "The clip to play on awake."));
					EditorGUILayout.PropertyField(defaultDelay, new GUIContent("Default Delay", "The delay between the scene starting and the clip playing."));
				}
				EditorGUILayout.EndFadeGroup();
				EditorGUILayout.PropertyField(loop, new GUIContent("Loop Clip", "If true, will make any played clip loop when it finishes."));
				EditorGUILayout.PropertyField(scaleAudioSpeed, new GUIContent("Scale Audio Speed", "Whether or not the speed of the audio will be slowed/sped up to match Time.timeScale."));
				EditorGUILayout.Space();
				GUILayout.Box("Phoneme Animation Settings", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(restTime, new GUIContent("Rest Time", "If there are no phonemes within this many seconds of the previous one, a rest will be inserted."));
				EditorGUILayout.PropertyField(restHoldTime, new GUIContent("Pre-Rest Hold Time", "The time, in seconds, a shape will be held before blending when a rest is inserted."));
				EditorGUILayout.PropertyField(phonemeCurveGenerationMode, new GUIContent("Phoneme Curve Generation Mode", "How tangents are generated for animations. Tight is more accurate, Loose is more natural."));
				EditorGUILayout.Space();
				GUILayout.Box("Emotion Animation Settings", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(emotionCurveGenerationMode, new GUIContent("Emotion Curve Generation Mode", "How tangents are generated for animations. Tight is more accurate, Loose is more natural."));

				GUILayout.Space(20);

				EditorGUILayout.PropertyField(onFinishedPlaying);

				if (LipSyncEditorExtensions.oldToggle != LipSyncEditorExtensions.currentToggle && LipSyncEditorExtensions.currentTarget == lsTarget)
				{

					if (LipSyncEditorExtensions.oldToggle > -1)
					{
						if (markerTab == 0)
						{
							if (lsTarget.useBones)
							{
								foreach (BoneShape boneshape in lsTarget.phonemes[LipSyncEditorExtensions.oldToggle].bones)
								{
									if (boneshape.bone != null)
									{
										boneshape.bone.localPosition = boneshape.neutralPosition;
										boneshape.bone.localEulerAngles = boneshape.neutralRotation;
										boneshape.bone.localScale = boneshape.neutralScale;
									}
								}
							}

							foreach (PhonemeShape shape in lsTarget.phonemes)
							{
								foreach (int blendable in shape.blendShapes)
								{
									lsTarget.blendSystem.SetBlendableValue(blendable, 0);
								}
							}
						}
						else if (markerTab == 1)
						{
							if (lsTarget.useBones)
							{
								foreach (BoneShape boneshape in lsTarget.emotions[LipSyncEditorExtensions.oldToggle].bones)
								{
									if (boneshape.bone != null)
									{
										boneshape.bone.localPosition = boneshape.neutralPosition;
										boneshape.bone.localEulerAngles = boneshape.neutralRotation;
										boneshape.bone.localScale = boneshape.neutralScale;
									}
								}
							}

							foreach (EmotionShape shape in lsTarget.emotions)
							{
								foreach (int blendable in shape.blendShapes)
								{
									lsTarget.blendSystem.SetBlendableValue(blendable, 0);
								}
							}
						}
					}

					if (LipSyncEditorExtensions.currentToggle > -1)
					{
						if (markerTab == 0)
						{
							if (lsTarget.useBones)
							{
								foreach (BoneShape boneshape in lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].bones)
								{
									if (boneshape.bone != null)
									{
										boneshape.bone.localPosition = boneshape.endPosition;
										boneshape.bone.localEulerAngles = boneshape.endRotation;
										boneshape.bone.localScale = boneshape.endScale;
									}
								}
							}

							for (int b = 0; b < lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].blendShapes.Count; b++)
							{
								lsTarget.blendSystem.SetBlendableValue(lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].blendShapes[b], lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].weights[b]);
							}
						}
						else if (markerTab == 1)
						{
							lsTarget.DisplayEmotionPose(LipSyncEditorExtensions.currentToggle, 1);
						}
					}

					LipSyncEditorExtensions.oldToggle = LipSyncEditorExtensions.currentToggle;
				}

				if (EditorGUI.EndChangeCheck())
				{
					if (lsTarget.onSettingsChanged != null)
					{
						lsTarget.onSettingsChanged.Invoke();
					}
				}

				if (GUI.changed)
				{
					if (blendables == null)
					{
						GetBlendShapes();
					}

					if (LipSyncEditorExtensions.currentToggle > -1 && LipSyncEditorExtensions.currentTarget == lsTarget)
					{
						if (markerTab == 0)
						{
							for (int b = 0; b < lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].blendShapes.Count; b++)
							{
								lsTarget.blendSystem.SetBlendableValue(lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].blendShapes[b], lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].weights[b]);
							}
						}
						else if (markerTab == 1)
						{
							for (int b = 0; b < lsTarget.emotions[LipSyncEditorExtensions.currentToggle].blendShapes.Count; b++)
							{
								lsTarget.blendSystem.SetBlendableValue(lsTarget.emotions[LipSyncEditorExtensions.currentToggle].blendShapes[b], lsTarget.emotions[LipSyncEditorExtensions.currentToggle].weights[b]);
							}
						}
					}

					EditorUtility.SetDirty(lsTarget);
					serializedTarget.SetIsDifferentCacheDirty();
				}
			}
			else
			{
				EditorGUILayout.HelpBox(lsTarget.blendSystem.notReadyMessage, MessageType.Warning);
			}
		}
		EditorGUI.EndDisabledGroup();
		EditorGUILayout.EndVertical();

		if (saving)
		{
			GUI.Box(new Rect(40, fullheight.y + (fullheight.height / 2) - 60, fullheight.width - 80, 120), "", (GUIStyle)"flow node 0");
			GUI.Box(new Rect(50, fullheight.y + (fullheight.height / 2) - 50, fullheight.width - 100, 20), "Create New Preset", EditorStyles.label);
			GUI.Box(new Rect(50, fullheight.y + (fullheight.height / 2) - 20, 80, 20), "Preset Path", EditorStyles.label);
			savingName = EditorGUI.TextField(new Rect(140, fullheight.y + (fullheight.height / 2) - 20, fullheight.width - 290, 20), "", savingName);

			if (GUI.Button(new Rect(fullheight.width - 140, fullheight.y + (fullheight.height / 2) - 20, 80, 20), "Browse"))
			{
				GUI.FocusControl("");
				string newPath = EditorUtility.SaveFilePanelInProject("Chose Preset Location", "New Preset", "asset", "");

				if (newPath != "")
				{
					savingName = newPath.Substring("Assets/".Length);
				}
			}
			if (GUI.Button(new Rect(100, fullheight.y + (fullheight.height / 2) + 15, (fullheight.width / 2) - 110, 25), "Cancel"))
			{
				GUI.FocusControl("");
				savingName = "";
				saving = false;
			}
			if (GUI.Button(new Rect((fullheight.width / 2) + 10, fullheight.y + (fullheight.height / 2) + 15, (fullheight.width / 2) - 110, 25), "Save"))
			{
				if (!Path.GetDirectoryName(savingName).Contains("Presets"))
				{
					EditorUtility.DisplayDialog("Invalid Path", "Presets must be saved in a folder called Presets, or a subfolder of one.", "OK");
					return;
				}
				else if (!Directory.Exists(Application.dataPath + "/" + Path.GetDirectoryName(savingName)))
				{
					EditorUtility.DisplayDialog("Directory Does Not Exist", "The directory " + Path.GetDirectoryName(savingName) + " does not exist.", "OK");
					return;
				}
				else if (!Path.HasExtension(savingName))
				{
					savingName = Path.GetDirectoryName(savingName) + "/" + Path.GetFileNameWithoutExtension(savingName) + ".asset";
				}
				else if (Path.GetExtension(savingName) != ".asset")
				{
					savingName = Path.GetDirectoryName(savingName) + "/" + Path.GetFileNameWithoutExtension(savingName) + ".asset";
				}

				LipSyncPreset preset = ScriptableObject.CreateInstance<LipSyncPreset>();
				preset.phonemeShapes = new LipSyncPreset.PhonemeShapeInfo[lsTarget.phonemes.Count];
				preset.emotionShapes = new LipSyncPreset.EmotionShapeInfo[lsTarget.emotions.Count];

				// Add phonemes
				for (int p = 0; p < lsTarget.phonemes.Count; p++)
				{
					LipSyncPreset.PhonemeShapeInfo phonemeInfo = new LipSyncPreset.PhonemeShapeInfo();
					phonemeInfo.phonemeName = lsTarget.phonemes[p].phonemeName;
					phonemeInfo.blendables = new LipSyncPreset.BlendableInfo[lsTarget.phonemes[p].blendShapes.Count];
					phonemeInfo.bones = new LipSyncPreset.BoneInfo[lsTarget.phonemes[p].bones.Count];

					// Add blendables
					for (int b = 0; b < lsTarget.phonemes[p].blendShapes.Count; b++)
					{
						LipSyncPreset.BlendableInfo blendable = new LipSyncPreset.BlendableInfo();
						blendable.blendableNumber = lsTarget.phonemes[p].blendShapes[b];
						blendable.blendableName = blendables[lsTarget.phonemes[p].blendShapes[b]];
						blendable.weight = lsTarget.phonemes[p].weights[b];

						phonemeInfo.blendables[b] = blendable;
					}

					// Add bones
					for (int b = 0; b < lsTarget.phonemes[p].bones.Count; b++)
					{
						LipSyncPreset.BoneInfo bone = new LipSyncPreset.BoneInfo();
						bone.name = lsTarget.phonemes[p].bones[b].bone.name;
						bone.localPosition = lsTarget.phonemes[p].bones[b].endPosition;
						bone.localRotation = lsTarget.phonemes[p].bones[b].endRotation;
						bone.lockPosition = lsTarget.phonemes[p].bones[b].lockPosition;
						bone.lockRotation = lsTarget.phonemes[p].bones[b].lockRotation;

						string path = "";
						Transform level = lsTarget.phonemes[p].bones[b].bone.parent;
						while (level != null)
						{
							path += level.name + "/";
							level = level.parent;
						}
						bone.path = path;

						phonemeInfo.bones[b] = bone;
					}

					preset.phonemeShapes[p] = phonemeInfo;
				}

				// Add emotions
				for (int e = 0; e < lsTarget.emotions.Count; e++)
				{
					LipSyncPreset.EmotionShapeInfo emotionInfo = new LipSyncPreset.EmotionShapeInfo();
					emotionInfo.emotion = lsTarget.emotions[e].emotion;
					emotionInfo.blendables = new LipSyncPreset.BlendableInfo[lsTarget.emotions[e].blendShapes.Count];
					emotionInfo.bones = new LipSyncPreset.BoneInfo[lsTarget.emotions[e].bones.Count];

					// Add blendables
					for (int b = 0; b < lsTarget.emotions[e].blendShapes.Count; b++)
					{
						LipSyncPreset.BlendableInfo blendable = new LipSyncPreset.BlendableInfo();
						blendable.blendableNumber = lsTarget.emotions[e].blendShapes[b];
						blendable.blendableName = blendables[lsTarget.emotions[e].blendShapes[b]];
						blendable.weight = lsTarget.emotions[e].weights[b];

						emotionInfo.blendables[b] = blendable;
					}

					// Add bones
					for (int b = 0; b < lsTarget.emotions[e].bones.Count; b++)
					{
						LipSyncPreset.BoneInfo bone = new LipSyncPreset.BoneInfo();
						bone.name = lsTarget.emotions[e].bones[b].bone.name;
						bone.localPosition = lsTarget.emotions[e].bones[b].endPosition;
						bone.localRotation = lsTarget.emotions[e].bones[b].endRotation;
						bone.lockPosition = lsTarget.emotions[e].bones[b].lockPosition;
						bone.lockRotation = lsTarget.emotions[e].bones[b].lockRotation;

						string path = "";
						Transform level = lsTarget.emotions[e].bones[b].bone.parent;
						while (level != null)
						{
							path += level.name + "/";
							level = level.parent;
						}
						bone.path = path;

						emotionInfo.bones[b] = bone;
					}
					preset.emotionShapes[e] = emotionInfo;
				}

				AssetDatabase.CreateAsset(preset, "Assets/" + savingName);
				AssetDatabase.Refresh();
				savingName = "";
				saving = false;
			}
		}

		serializedTarget.ApplyModifiedProperties();
	}

	void OnSceneGUI ()
	{
		if (markerTab == 0 && LipSyncEditorExtensions.currentToggle >= 0)
		{
			Handles.BeginGUI();
			if (LipSyncEditorExtensions.currentToggle < settings.phonemeSet.guideImages.Length)
				GUI.Box(new Rect(Screen.width - 256, Screen.height - 246, 256, 256), settings.phonemeSet.guideImages[LipSyncEditorExtensions.currentToggle], GUIStyle.none);
			Handles.EndGUI();
		}

		// Bone Handles
		if (lsTarget.useBones && LipSyncEditorExtensions.currentToggle >= 0 && LipSyncEditorExtensions.currentTarget == lsTarget)
		{
			BoneShape bone = null;
			if (markerTab == 0)
			{
				if (LipSyncEditorExtensions.selectedBone < lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].bones.Count && lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].bones.Count > 0)
				{
					bone = lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].bones[LipSyncEditorExtensions.selectedBone];
				}
				else
				{
					return;
				}
			}
			else if (markerTab == 1)
			{
				if (LipSyncEditorExtensions.selectedBone < lsTarget.emotions[LipSyncEditorExtensions.currentToggle].bones.Count && lsTarget.emotions[LipSyncEditorExtensions.currentToggle].bones.Count > 0)
				{
					bone = lsTarget.emotions[LipSyncEditorExtensions.currentToggle].bones[LipSyncEditorExtensions.selectedBone];
				}
				else
				{
					return;
				}
			}
			if (bone.bone == null)
				return;

			if (Tools.current == Tool.Move)
			{
				Undo.RecordObject(bone.bone, "Move");

				Vector3 change = Handles.PositionHandle(bone.bone.position, bone.bone.rotation);
				if (change != bone.bone.position)
				{
					bone.bone.position = change;
					bone.endPosition = bone.bone.localPosition;
				}
			}
			else if (Tools.current == Tool.Rotate)
			{
				Undo.RecordObject(bone.bone, "Rotate");
				Quaternion change = Handles.RotationHandle(bone.bone.rotation, bone.bone.position);
				if (change != bone.bone.rotation)
				{
					bone.bone.rotation = change;
					bone.endRotation = bone.bone.localEulerAngles;
				}
			}
			else if (Tools.current == Tool.Scale)
			{
				Undo.RecordObject(bone.bone, "Scale");
				Vector3 change = Handles.ScaleHandle(bone.bone.localScale, bone.bone.position, bone.bone.rotation, HandleUtility.GetHandleSize(bone.bone.position));
				if (change != bone.bone.localScale)
				{
					bone.bone.localScale = change;
					bone.endScale = bone.bone.localScale;
				}
			}

		}
	}

	void LoadPreset (object data)
	{
		string file = (string)data;
		if (file.EndsWith(".asset", true, null))
		{
			LipSyncPreset preset = AssetDatabase.LoadAssetAtPath<LipSyncPreset>("Assets" + file.Substring((Application.dataPath).Length));

			if (preset != null)
			{

				if (lsTarget.phonemes.Count > preset.phonemeShapes.Length)
				{
					if (!EditorUtility.DisplayDialog("Loading Preset may cause data loss!", "This Preset contains fewer Phonemes than the LipSync character you're applying it to! Loading it will cause the missing phoneme poses to be lost.", "Load Anyway", "Cancel"))
						return;
				}

				if (lsTarget.emotions.Count > preset.emotionShapes.Length)
				{
					if (!EditorUtility.DisplayDialog("Loading Preset may cause data loss!", "This Preset contains fewer Emotions than the LipSync character you're applying it to! Loading it will cause the missing emotion poses to be lost.", "Load Anyway", "Cancel"))
						return;
				}

				List<PhonemeShape> newPhonemes = new List<PhonemeShape>();
				List<EmotionShape> newEmotions = new List<EmotionShape>();

				// Phonemes
				for (int shape = 0; shape < preset.phonemeShapes.Length; shape++)
				{
					string phonemeName = preset.phonemeShapes[shape].phonemeName;

					if (string.IsNullOrEmpty(phonemeName))
						phonemeName = preset.phonemeShapes[shape].phoneme.ToString();

					newPhonemes.Add(new PhonemeShape(phonemeName));

					for (int blendable = 0; blendable < preset.phonemeShapes[shape].blendables.Length; blendable++)
					{
						int finalBlendable = preset.FindBlendable(preset.phonemeShapes[shape].blendables[blendable], lsTarget.blendSystem);
						if (finalBlendable >= 0)
						{
							newPhonemes[shape].blendShapes.Add(finalBlendable);
							newPhonemes[shape].weights.Add(preset.phonemeShapes[shape].blendables[blendable].weight);
							newPhonemes[shape].blendableNames.Add(lsTarget.blendSystem.GetBlendables()[finalBlendable]);
						}
					}

					for (int bone = 0; bone < preset.phonemeShapes[shape].bones.Length; bone++)
					{
						BoneShape newBone = new BoneShape();
						newBone.bone = preset.FindBone(preset.phonemeShapes[shape].bones[bone], lsTarget.transform);
						newBone.SetNeutral();
						newBone.endPosition = preset.phonemeShapes[shape].bones[bone].localPosition;
						newBone.endRotation = preset.phonemeShapes[shape].bones[bone].localRotation;
						newBone.lockPosition = preset.phonemeShapes[shape].bones[bone].lockPosition;
						newBone.lockRotation = preset.phonemeShapes[shape].bones[bone].lockRotation;

						newPhonemes[shape].bones.Add(newBone);
					}
				}

				// Emotion
				for (int shape = 0; shape < preset.emotionShapes.Length; shape++)
				{
					newEmotions.Add(new EmotionShape(preset.emotionShapes[shape].emotion));

					for (int blendable = 0; blendable < preset.emotionShapes[shape].blendables.Length; blendable++)
					{
						int finalBlendable = preset.FindBlendable(preset.emotionShapes[shape].blendables[blendable], lsTarget.blendSystem);
						if (finalBlendable >= 0)
						{
							newEmotions[shape].blendShapes.Add(finalBlendable);
							newEmotions[shape].weights.Add(preset.emotionShapes[shape].blendables[blendable].weight);
							newEmotions[shape].blendableNames.Add(lsTarget.blendSystem.GetBlendables()[finalBlendable]);
						}
					}

					for (int bone = 0; bone < preset.emotionShapes[shape].bones.Length; bone++)
					{
						BoneShape newBone = new BoneShape();
						newBone.bone = preset.FindBone(preset.emotionShapes[shape].bones[bone], lsTarget.transform);
						newBone.SetNeutral();
						newBone.endPosition = preset.emotionShapes[shape].bones[bone].localPosition;
						newBone.endRotation = preset.emotionShapes[shape].bones[bone].localRotation;
						newBone.lockPosition = preset.emotionShapes[shape].bones[bone].lockPosition;
						newBone.lockRotation = preset.emotionShapes[shape].bones[bone].lockRotation;

						newEmotions[shape].bones.Add(newBone);
					}
				}

				lsTarget.phonemes = newPhonemes;
				lsTarget.emotions = newEmotions;

				for (int bShape = 0; bShape < lsTarget.blendSystem.blendableCount; bShape++)
				{
					lsTarget.blendSystem.SetBlendableValue(bShape, 0);
				}

				if (markerTab == 0)
				{
					if (LipSyncEditorExtensions.currentToggle >= 0)
					{
						int b = 0;
						foreach (int shape in lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].blendShapes)
						{
							lsTarget.blendSystem.SetBlendableValue(shape, lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].weights[b]);
							b++;
						}
					}
				}
				else if (markerTab == 1)
				{
					if (LipSyncEditorExtensions.currentToggle >= 0)
					{
						int b = 0;
						foreach (int shape in lsTarget.emotions[LipSyncEditorExtensions.currentToggle].blendShapes)
						{
							lsTarget.blendSystem.SetBlendableValue(shape, lsTarget.emotions[LipSyncEditorExtensions.currentToggle].weights[b]);
							b++;
						}
					}
				}
			}
		}
	}

	void NewPreset ()
	{
		saving = true;
		savingName = "Rogo Digital/LipSync Pro/Presets/New Preset.asset";
	}

	void GetBlendShapes ()
	{
		if (lsTarget.blendSystem.isReady)
		{
			blendables = lsTarget.blendSystem.GetBlendables();
		}
	}

	public static void FixedEndFadeGroup (float value)
	{
		if (value == 0f || value == 1f)
		{
			return;
		}
		EditorGUILayout.EndFadeGroup();
	}

	private void AutoUpdate (float oldVersion)
	{
		// Used for additional future-proofing
		if (oldVersion < 0.6f)
		{
			// Update new rest time values
			if (EditorUtility.DisplayDialog("LipSync has been updated.", "This character was last used with an old version of LipSync prior to 0.6. The recommended values for Rest Time and Pre-Rest Hold Time have been changed to 0.2 and 0.4 respectively. Do you want to change these values automatically?", "Yes", "No"))
			{
				lsTarget.restTime = 0.2f;
				lsTarget.restHoldTime = 0.4f;
			}
		}

		if (oldVersion < 1.3f)
		{
			// Switch to new phoneme format
			for (int p = 0; p < lsTarget.phonemes.Count; p++)
			{
				lsTarget.phonemes[p].phonemeName = lsTarget.phonemes[p].phoneme.ToString();
			}
		}
	}
}