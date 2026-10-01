import AVFoundation

@main
struct CameraDeviceSelectorTests {
    static func main() {
        let types = CameraDeviceSelector.preferredDeviceTypes
#if os(iOS)
        precondition(types.first == .builtInTripleCamera)
        precondition(types.contains(.builtInDualWideCamera))
#endif
        precondition(types.last == .builtInWideAngleCamera)
        print("CameraDeviceSelectorTests passed")
    }
}
