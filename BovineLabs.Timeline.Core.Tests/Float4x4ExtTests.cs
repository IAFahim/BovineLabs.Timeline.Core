using BovineLabs.Timeline.Core;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Core.Tests
{
    [TestFixture]
    public class Float4x4ExtTests
    {
        [Test]
        public void ExtractLocalTransform_Identity_ReturnsIdentity()
        {
            var m = float4x4.identity;
            m.ExtractLocalTransform(out var lt);

            Assert.AreEqual(0f, lt.Position.x, 0.0001f);
            Assert.AreEqual(0f, lt.Position.y, 0.0001f);
            Assert.AreEqual(0f, lt.Position.z, 0.0001f);
            Assert.AreEqual(1f, lt.Scale, 0.0001f);
            Assert.AreEqual(quaternion.identity.value.x, lt.Rotation.value.x, 0.0001f);
            Assert.AreEqual(quaternion.identity.value.y, lt.Rotation.value.y, 0.0001f);
            Assert.AreEqual(quaternion.identity.value.z, lt.Rotation.value.z, 0.0001f);
            Assert.AreEqual(quaternion.identity.value.w, lt.Rotation.value.w, 0.0001f);
        }

        [Test]
        public void ExtractLocalTransform_TranslationOnly_PositionCorrect()
        {
            var m = float4x4.Translate(new float3(1, 2, 3));
            m.ExtractLocalTransform(out var lt);

            Assert.AreEqual(1f, lt.Position.x, 0.0001f);
            Assert.AreEqual(2f, lt.Position.y, 0.0001f);
            Assert.AreEqual(3f, lt.Position.z, 0.0001f);
            Assert.AreEqual(1f, lt.Scale, 0.0001f);
        }

        [Test]
        public void ExtractLocalTransform_ScaleOnly_UniformScaleCorrect()
        {
            var m = float4x4.Scale(2f);
            m.ExtractLocalTransform(out var lt);

            Assert.AreEqual(0f, lt.Position.x, 0.0001f);
            Assert.AreEqual(0f, lt.Position.y, 0.0001f);
            Assert.AreEqual(0f, lt.Position.z, 0.0001f);
            Assert.AreEqual(2f, lt.Scale, 0.0001f);
        }

        [Test]
        public void ExtractLocalTransform_RotationOnly_90DegYaw()
        {
            var m = float4x4.RotateY(math.PI / 2f);
            m.ExtractLocalTransform(out var lt);

            Assert.AreEqual(0f, lt.Position.x, 0.0001f);
            Assert.AreEqual(0f, lt.Position.y, 0.0001f);
            Assert.AreEqual(0f, lt.Position.z, 0.0001f);
            Assert.AreEqual(1f, lt.Scale, 0.0001f);

            var expected = quaternion.RotateY(math.PI / 2f);
            Assert.AreEqual(expected.value.x, lt.Rotation.value.x, 0.001f);
            Assert.AreEqual(expected.value.y, lt.Rotation.value.y, 0.001f);
            Assert.AreEqual(expected.value.z, lt.Rotation.value.z, 0.001f);
            Assert.AreEqual(expected.value.w, lt.Rotation.value.w, 0.001f);
        }

        [Test]
        public void ExtractLocalTransform_TranslationAndScale_BothCorrect()
        {
            var m = new float4x4(
                new float4(3f, 0f, 0f, 0f),
                new float4(0f, 3f, 0f, 0f),
                new float4(0f, 0f, 3f, 0f),
                new float4(5f, 10f, 15f, 1f));
            m.ExtractLocalTransform(out var lt);

            Assert.AreEqual(5f, lt.Position.x, 0.0001f);
            Assert.AreEqual(10f, lt.Position.y, 0.0001f);
            Assert.AreEqual(15f, lt.Position.z, 0.0001f);
            Assert.AreEqual(3f, lt.Scale, 0.0001f);
        }

        [Test]
        public void ExtractLocalTransform_NonUniformScale_UsesXAsScale()
        {
            var m = new float4x4(
                new float4(2f, 0f, 0f, 0f),
                new float4(0f, 4f, 0f, 0f),
                new float4(0f, 0f, 6f, 0f),
                new float4(0f, 0f, 0f, 1f));
            m.ExtractLocalTransform(out var lt);

            Assert.AreEqual(2f, lt.Scale, 0.0001f);
        }

        [Test]
        public void ExtractLocalTransform_TRS_AllCorrect()
        {
            var t = new float3(1, 2, 3);
            var r = quaternion.RotateY(math.PI / 4f);
            var s = 2.5f;
            var m = float4x4.TRS(t, r, new float3(s));

            m.ExtractLocalTransform(out var lt);

            Assert.AreEqual(t.x, lt.Position.x, 0.001f);
            Assert.AreEqual(t.y, lt.Position.y, 0.001f);
            Assert.AreEqual(t.z, lt.Position.z, 0.001f);
            Assert.AreEqual(s, lt.Scale, 0.001f);
        }

        [Test]
        public void ExtractLocalTransform_ZeroMatrix_DoesNotThrow()
        {
            var m = new float4x4(0f);
            Assert.DoesNotThrow(() => m.ExtractLocalTransform(out _));
        }

        [Test]
        public void ExtractLocalTransform_NegativeScale_ExtractsAbsScale()
        {
            var m = new float4x4(
                new float4(-2f, 0f, 0f, 0f),
                new float4(0f, 2f, 0f, 0f),
                new float4(0f, 0f, 2f, 0f),
                new float4(0f, 0f, 0f, 1f));
            m.ExtractLocalTransform(out var lt);

            Assert.AreEqual(2f, lt.Scale, 0.0001f);
        }

        [Test]
        public void ExtractLocalTransform_SmallScale_HandledCorrectly()
        {
            var m = float4x4.Scale(0.01f);
            m.ExtractLocalTransform(out var lt);

            Assert.AreEqual(0.01f, lt.Scale, 0.0001f);
        }
    }
}
