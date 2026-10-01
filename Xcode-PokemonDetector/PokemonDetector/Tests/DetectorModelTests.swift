import Foundation

@main
struct DetectorModelTests {
    static func main() {
        testModelMetadata()
        testInitialFailureCanRetryAndActivateFallback()
        print("DetectorModelTests passed")
    }

    private static func testModelMetadata() {
        precondition(DetectorModel.allCases == [.baseline3900, .mixedDevice6000])
        precondition(DetectorModel.defaultSelection == .mixedDevice6000)
        precondition(DetectorModel.baseline3900.displayName == "Baseline 3900")
        precondition(DetectorModel.mixedDevice6000.displayName == "Mixed Device 6000")
        precondition(DetectorModel.baseline3900.shortName == "Baseline")
        precondition(DetectorModel.mixedDevice6000.shortName == "Mixed 6000")
        precondition(DetectorModel.baseline3900.resourceName == "best")
        precondition(DetectorModel.mixedDevice6000.resourceName == "MixedDevice6000")
        precondition(Set(DetectorModel.allCases.map(\.displayName)).count == 2)
        precondition(Set(DetectorModel.allCases.map(\.resourceName)).count == 2)
    }

    private static func testInitialFailureCanRetryAndActivateFallback() {
        var state = DetectorSelectionState.initial
        precondition(state.active == nil)
        precondition(state.loading == .mixedDevice6000)
        precondition(state.isSwitching)
        precondition(!state.beginLoading(.baseline3900))

        state.finishLoading(.mixedDevice6000, succeeded: false)
        precondition(state.active == nil)
        precondition(state.loading == nil)
        precondition(!state.isSwitching)
        precondition(state.beginLoading(.mixedDevice6000))

        state.finishLoading(.mixedDevice6000, succeeded: false)
        precondition(state.beginLoading(.baseline3900))
        state.finishLoading(.baseline3900, succeeded: true)
        precondition(state.active == .baseline3900)
        precondition(!state.beginLoading(.baseline3900))
    }
}
