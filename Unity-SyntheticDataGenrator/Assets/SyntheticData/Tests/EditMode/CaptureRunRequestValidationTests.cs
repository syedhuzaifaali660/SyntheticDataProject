using System.IO;
using NUnit.Framework;
using SyntheticData.Editor;
using SyntheticData.Output;

namespace SyntheticData.Tests
{
    public sealed class CaptureRunRequestValidationTests
    {
        [TestCase("smoke-20")]
        [TestCase("run_20260830")]
        public void ValidatesACompleteRequestWithASafeRunId(string runId)
        {
            var request = new CaptureRunRequest(runId, "Generated", 20, 42);

            Assert.That(CaptureRunRequestValidator.Validate(request), Is.Empty);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("nested/run")]
        [TestCase("nested\\run")]
        [TestCase(".")]
        [TestCase("..")]
        public void RejectsEmptyOrPathLikeRunIds(string runId)
        {
            var request = new CaptureRunRequest(runId, "Generated", 20, 42);

            Assert.That(
                CaptureRunRequestValidator.Validate(request),
                Does.Contain("run ID"));
        }

        [TestCase("")]
        [TestCase("  ")]
        public void RejectsAnEmptyOutputRoot(string outputRoot)
        {
            var request = new CaptureRunRequest("smoke", outputRoot, 20, 42);

            Assert.That(
                CaptureRunRequestValidator.Validate(request),
                Does.Contain("output root"));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsNonPositiveFrameCounts(int frameCount)
        {
            var request = new CaptureRunRequest("smoke", "Generated", frameCount, 42);

            Assert.That(
                CaptureRunRequestValidator.Validate(request),
                Does.Contain("count"));
        }

        [Test]
        public void DefaultOutputRootTargetsTheRepositoryPythonDatasetFolder()
        {
            var outputRoot = CaptureRunPaths.GetDefaultOutputRoot();

            Assert.That(Path.IsPathFullyQualified(outputRoot), Is.True);
            Assert.That(
                outputRoot.Replace('\\', '/'),
                Does.EndWith("/Python-ModelTraining/data/generated"));
        }

        [Test]
        public void ParsesBatchCaptureArguments()
        {
            var arguments = new[]
            {
                "Unity",
                "-captureRunId", "visual-review-100",
                "-captureFrameCount", "100",
                "-captureSeed", "42",
                "-captureOutputRoot", "Generated"
            };

            var parsed = CaptureRunCommand.TryParseRequest(
                arguments,
                out var request,
                out var error);

            Assert.That(parsed, Is.True, error);
            Assert.That(request.RunId, Is.EqualTo("visual-review-100"));
            Assert.That(request.FrameCount, Is.EqualTo(100));
            Assert.That(request.Seed, Is.EqualTo(42));
            Assert.That(request.OutputRoot, Is.EqualTo("Generated"));
        }

        [Test]
        public void ParsesMixedDeviceProfilesFlag()
        {
            var arguments = new[]
            {
                "Unity", "-captureRunId", "mixed", "-captureFrameCount", "20",
                "-captureOutputRoot", "Generated", "-captureMixedDeviceProfiles"
            };

            Assert.That(CaptureRunCommand.TryParseRequest(arguments, out var request, out var error), Is.True, error);
            Assert.That(request.MixedDeviceProfiles, Is.True);
        }

        [Test]
        public void BatchCaptureArgumentsRequireRunIdAndPositiveIntegerCount()
        {
            var arguments = new[]
            {
                "Unity",
                "-captureFrameCount", "many"
            };

            var parsed = CaptureRunCommand.TryParseRequest(
                arguments,
                out _,
                out var error);

            Assert.That(parsed, Is.False);
            Assert.That(error, Does.Contain("captureRunId"));
        }

        [TestCase(".")]
        [TestCase("..")]
        [TestCase("nested/run")]
        [TestCase("nested\\run")]
        public void RuntimeRunDirectoryPolicyRejectsTraversal(string runId)
        {
            Assert.That(
                () => RunDirectoryPolicy.Resolve("Generated", runId),
                Throws.ArgumentException);
        }

        [Test]
        public void RuntimeRunDirectoryPolicyKeepsSafeRunUnderNormalizedRoot()
        {
            var root = Path.Combine(".", "Generated");
            var resolved = RunDirectoryPolicy.Resolve(root, "smoke-20");

            Assert.That(
                Path.GetDirectoryName(resolved),
                Is.EqualTo(Path.GetFullPath(root)));
        }
    }
}
