// ============================================================================
// PlayMode Tests — Integration Tests
//
// Tests end-to-end execution combining CompositionRoot, Domain, Infrastructure,
// and Application layers running inside the Unity engine.
// ============================================================================

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HVOFSim.Presentation;
using HVOFSim.Presentation.Scene;

namespace HVOFSim.Tests.PlayMode
{
    public class IntegrationTests
    {
        private GameObject _appGO;
        private CompositionRoot _compositionRoot;

        [SetUp]
        public void Setup()
        {
            // Create a temporary game object and attach the composition root
            _appGO = new GameObject("HVOF_App_Test");
            _compositionRoot = _appGO.AddComponent<CompositionRoot>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_appGO != null)
            {
                Object.DestroyImmediate(_appGO);
            }
        }

        [UnityTest]
        public IEnumerator CompositionRoot_BootstrapsSuccessfully()
        {
            // Allow one frame for Awake() to complete
            yield return null;

            // Verify scene objects were created
            var labScene = GameObject.Find("LabScene");
            Assert.IsNotNull(labScene, "LabScene GameObject should be created.");

            var sceneBuilder = labScene.GetComponent<LabSceneBuilder>();
            Assert.IsNotNull(sceneBuilder, "LabSceneBuilder should be attached.");
            Assert.IsNotNull(sceneBuilder.Chamber, "Chamber should be built.");
            Assert.IsNotNull(sceneBuilder.Table, "Table should be built.");
            Assert.IsNotNull(sceneBuilder.LaserHead, "LaserHead should be built.");

            var dashboard = GameObject.Find("Dashboard");
            Assert.IsNotNull(dashboard, "Dashboard GameObject should be created.");
        }
    }
}
