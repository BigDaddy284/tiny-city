using System.Collections;
using NUnit.Framework;
using TinyTown.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace TinyTown.Tests.PlayMode
{
    public class BootstrapTests
    {
        [UnityTest]
        public IEnumerator EnteringPlayMode_CreatesBootstrapCameraAndValidSettings()
        {
            yield return null;

            var bootstrap = Object.FindAnyObjectByType<Bootstrap>();
            Assert.That(bootstrap, Is.Not.Null, "Bootstrap was not created on startup.");
            Assert.That(bootstrap.gameObject.name, Is.EqualTo(Bootstrap.ObjectName));
            Assert.That(Camera.main, Is.Not.Null, "No main camera exists after startup.");
            Assert.That(bootstrap.Settings, Is.Not.Null);
            Assert.That(bootstrap.Settings.Validate(), Is.Empty);
        }
    }
}
