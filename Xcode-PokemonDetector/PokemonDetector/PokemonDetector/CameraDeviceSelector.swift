import AVFoundation

enum CameraDeviceSelector {
#if os(iOS)
    static let preferredDeviceTypes: [AVCaptureDevice.DeviceType] = [
        .builtInTripleCamera,
        .builtInDualWideCamera,
        .builtInDualCamera,
        .builtInWideAngleCamera,
    ]
#else
    static let preferredDeviceTypes: [AVCaptureDevice.DeviceType] = [.builtInWideAngleCamera]
#endif

    static func backCamera() -> AVCaptureDevice? {
        let devices = AVCaptureDevice.DiscoverySession(
            deviceTypes: preferredDeviceTypes,
            mediaType: .video,
            position: .back
        ).devices

        return preferredDeviceTypes.lazy.compactMap { type in
            devices.first { $0.deviceType == type }
        }.first
    }
}
