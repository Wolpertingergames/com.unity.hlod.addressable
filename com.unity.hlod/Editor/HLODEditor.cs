﻿using System;
using System.Collections;
using System.Linq;
using Unity.HLODSystem.Utils;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.HLODSystem
{
    [CustomEditor(typeof(HLOD))]
    public class HLODEditor : Editor
    {
        public static class Styles
        {
            public static GUIContent GenerateButtonEnable = new GUIContent("Generate", "Generate a HLOD mesh.");
            public static GUIContent GenerateButtonDisable = new GUIContent("Generate", "Generate is only allow in prefab mode.");
            public static GUIContent GenerateButtonExists = new GUIContent("Generate", "HLOD already generated.");
            public static GUIContent UpdateButtonEnable = new GUIContent("Update", "Update a HLOD mesh.");
            public static GUIContent UpdateButtonDisable = new GUIContent("Update", "Update is only allow in prefab mode.");
            public static GUIContent UpdateButtonNotExists = new GUIContent("Update", "You need to generate HLOD before the update.");
            public static GUIContent DestroyButtonEnable = new GUIContent("Destroy", "Destory a HLOD mesh.");
            public static GUIContent DestroyButtonDisable = new GUIContent("Destroy", "Destory is only allow in prefab mode.");
            public static GUIContent DestroyButtonNotExists = new GUIContent("Destroy", "You need to generate HLOD before the destroy.");
        }
        private SerializedProperty m_RecursiveGenerationProperty;
        private SerializedProperty m_MinSizeProperty;
        private SerializedProperty m_LODDistanceProperty;
        private SerializedProperty m_CullDistanceProperty;
        private SerializedProperty m_ThresholdSizeProperty;

        private LODSlider m_LODSlider;

        private Type[] m_BatcherTypes;
        private string[] m_BatcherNames;

        private Type[] m_SimplifierTypes;
        private string[] m_SimplifierNames;

        private Type[] m_StreamingTypes;
        private string[] m_StreamingNames;

        [InitializeOnLoadMethod]
        static void InitTagTagUtils()
        {
            if (LayerMask.NameToLayer(HLOD.HLODLayerStr) == -1)
            {
                Utils.TagUtils.AddLayer(HLOD.HLODLayerStr);
                Tools.lockedLayers |= LayerMask.NameToLayer(HLOD.HLODLayerStr);
            }
        }

        void OnEnable()
        {
            m_RecursiveGenerationProperty = serializedObject.FindProperty("m_RecursiveGeneration");
            m_MinSizeProperty = serializedObject.FindProperty("m_MinSize");
            m_LODDistanceProperty = serializedObject.FindProperty("m_LODDistance");
            m_CullDistanceProperty = serializedObject.FindProperty("m_CullDistance");
            m_ThresholdSizeProperty = serializedObject.FindProperty("m_ThresholdSize");

            m_LODSlider = new LODSlider(true, "Cull");
            m_LODSlider.InsertRange("High", m_LODDistanceProperty);
            m_LODSlider.InsertRange("Low", m_CullDistanceProperty);

            m_BatcherTypes = BatcherTypes.GetTypes();
            m_BatcherNames = m_BatcherTypes.Select(t => t.Name).ToArray();

            m_SimplifierTypes = Simplifier.SimplifierTypes.GetTypes();
            m_SimplifierNames = m_SimplifierTypes.Select(t => t.Name).ToArray();

            m_StreamingTypes = Streaming.StreamingBuilderTypes.GetTypes();
            m_StreamingNames = m_StreamingTypes.Select(t => t.Name).ToArray();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            HLOD hlod = target as HLOD;
            if (hlod == null)
            {
                EditorGUILayout.LabelField("HLOD is null.");
                return;
            }

            EditorGUILayout.PropertyField(m_RecursiveGenerationProperty);
            if ( m_RecursiveGenerationProperty.boolValue )
            {
                EditorGUI.indentLevel += 1;
                EditorGUILayout.PropertyField(m_MinSizeProperty);
                EditorGUI.indentLevel -= 1;
            }

            m_LODSlider.Draw();
            EditorGUILayout.PropertyField(m_ThresholdSizeProperty);

            if (m_BatcherTypes.Length > 0)
            {
                int batcherIndex = Math.Max(Array.IndexOf(m_BatcherTypes, hlod.BatcherType), 0);
                batcherIndex = EditorGUILayout.Popup("Batcher", batcherIndex, m_BatcherNames);
                hlod.BatcherType = m_BatcherTypes[batcherIndex];

                var info = m_BatcherTypes[batcherIndex].GetMethod("OnGUI");
                if (info != null)
                {
                    if (info.IsStatic == true)
                    {
                        info.Invoke(null, new object[] {hlod});
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("Can not find Batchers.");
            }

            if (m_SimplifierTypes.Length > 0)
            {
                int simplifierIndex = Math.Max(Array.IndexOf(m_SimplifierTypes, hlod.SimplifierType), 0);
                simplifierIndex = EditorGUILayout.Popup("Simplifier", simplifierIndex, m_SimplifierNames);
                hlod.SimplifierType = m_SimplifierTypes[simplifierIndex];

                var info = m_SimplifierTypes[simplifierIndex].GetMethod("OnGUI");
                if (info != null)
                {
                    if (info.IsStatic == true)
                    {
                        info.Invoke(null, new object[] {hlod});
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("Can not find Simplifiers.");
            }


            if (m_StreamingTypes.Length > 0)
            {
                int streamingIndex = Math.Max(Array.IndexOf(m_StreamingTypes, hlod.StreamingType), 0);
                streamingIndex = EditorGUILayout.Popup("Streaming", streamingIndex, m_StreamingNames);
                hlod.StreamingType = m_StreamingTypes[streamingIndex];

                var info = m_StreamingTypes[streamingIndex].GetMethod("OnGUI");
                if (info != null)
                {
                    if (info.IsStatic == true)
                    {
                        info.Invoke(null, new object[] {hlod});
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("Can not find StreamingSetters.");
            }


            GUIContent generateButton = Styles.GenerateButtonEnable;
            GUIContent updateButton = Styles.UpdateButtonNotExists;
            GUIContent destroyButton = Styles.DestroyButtonNotExists;

            if (PrefabStageUtility.GetCurrentPrefabStage() == null ||
                PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot == null)
            {
                //generate is only allow in prefab mode.
                GUI.enabled = false;
                generateButton = Styles.GenerateButtonDisable;
                updateButton = Styles.UpdateButtonDisable;
                destroyButton = Styles.DestroyButtonDisable;
            }
            else if (hlod.HighRoot != null && hlod.LowRoot != null)
            {
                generateButton = Styles.GenerateButtonExists;
                updateButton = Styles.UpdateButtonEnable;
                destroyButton = Styles.DestroyButtonEnable;
            }



            GUI.enabled = generateButton == Styles.GenerateButtonEnable;
            if (GUILayout.Button(generateButton))
            {
                CoroutineRunner.RunCoroutine(HLODCreator.Create(hlod));
            }

            GUI.enabled = updateButton == Styles.UpdateButtonEnable;
            if (GUILayout.Button(updateButton))
            {
                CoroutineRunner.RunCoroutine(HLODCreator.Update(hlod));
            }

            GUI.enabled = destroyButton == Styles.DestroyButtonEnable;
            if (GUILayout.Button(destroyButton))
            {
                CoroutineRunner.RunCoroutine(HLODCreator.Destroy(hlod));
            }

            GUI.enabled = true;

            serializedObject.ApplyModifiedProperties();
            if (EditorGUI.EndChangeCheck())
            {
                if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                {
                    EditorSceneManager.MarkSceneDirty(PrefabStageUtility.GetCurrentPrefabStage().scene);
                }
            }
        }

    }

}