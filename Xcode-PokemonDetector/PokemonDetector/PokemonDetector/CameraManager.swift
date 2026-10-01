@preconcurrency import AVFoundation
import Combine
import Foundation

private struct SendableCameraFrame: @unchecked Sendable {
    let pixelBuffer: CVPixelBuffer
}

final class CameraManager: NSObject, ObservableObject {
    @Published private(set) var detections: [Detection] = []
    @Published private(set) var statusMessage: String = "Starting camera…"
    @Published private(set) var rotationAngle: CGFloat = 90
    @Published private(set) var zoomFactor: CGFloat = 1
    @Published private(set) var maximumZoom: CGFloat = 1
    @Published private(set) var sourceAspectRatio: CGFloat = 9.0 / 16.0
    @Published private(set) var inferenceFPS: Double = 0
    @Published private(set) var selectedModel: DetectorModel?
    @Published private(set) var isSwitchingModel = true

    let session = AVCaptureSession()

    private let sessionQueue = DispatchQueue(label: "pokemon.camera.session")
    private let outputQueue = DispatchQueue(label: "pokemon.camera.frames")
    private var detector: PokemonDetector?
    private var detectorCache: [DetectorModel: PokemonDetector] = [:]
    private var isInferenceRunning = false
    private var videoOutput: AVCaptureVideoDataOutput?
    private var cameraDevice: AVCaptureDevice?
    private var inferenceFPSCounter = InferenceFPSCounter()
    private var modelSelectionState = DetectorSelectionState.initial

    func start() {
        switch AVCaptureDevice.authorizationStatus(for: .video) {
        case .authorized:
            configureAndStart()
        case .notDetermined:
            AVCaptureDevice.requestAccess(for: .video) { [weak self] granted in
                DispatchQueue.main.async {
                    granted ? self?.configureAndStart() : self?.showPermissionDenied()
                }
            }
        default:
            showPermissionDenied()
        }
    }

    func stop() {
        sessionQueue.async { [session] in
            guard session.isRunning else { return }
            session.stopRunning()
        }
    }

    func updateLayout(width: CGFloat, height: CGFloat) {
        let newAngle = CameraRotationAngle.forLayout(width: width, height: height)
        guard rotationAngle != newAngle else { return }
        rotationAngle = newAngle
        sessionQueue.async { [weak self] in
            self?.videoOutput?.connection(with: .video)?.videoRotationAngle = newAngle
        }
    }

    func updateZoom(_ requestedZoom: CGFloat) {
        let newZoom = CameraZoom.clamp(requestedZoom, maximum: maximumZoom)
        zoomFactor = newZoom
        sessionQueue.async { [weak self] in
            guard let self, let camera = self.cameraDevice else { return }
            do {
                try camera.lockForConfiguration()
                camera.videoZoomFactor = CameraZoom.clamp(newZoom, maximum: min(3, camera.activeFormat.videoMaxZoomFactor))
                camera.unlockForConfiguration()
            } catch {
                self.publish(status: "Camera zoom could not be updated: \(error.localizedDescription)")
            }
        }
    }

    func selectModel(_ model: DetectorModel) {
        guard modelSelectionState.beginLoading(model) else { return }

        isSwitchingModel = modelSelectionState.isSwitching
        statusMessage = "Switching to \(model.displayName)…"

        outputQueue.async { [weak self] in
            guard let self else { return }
            DispatchQueue.main.async { [weak self] in
                self?.detections = []
            }
            do {
                let selectedDetector: PokemonDetector
                if let cachedDetector = self.detectorCache[model] {
                    selectedDetector = cachedDetector
                } else {
                    selectedDetector = try PokemonDetector(model: model)
                    self.detectorCache[model] = selectedDetector
                }
                self.detector = selectedDetector

                DispatchQueue.main.async { [weak self] in
                    guard let self else { return }
                    self.modelSelectionState.finishLoading(model, succeeded: true)
                    self.selectedModel = self.modelSelectionState.active
                    self.inferenceFPS = 0
                    self.isSwitchingModel = self.modelSelectionState.isSwitching
                    self.statusMessage = "Using \(model.displayName). Point the camera at Pikachu, Charmander, or Squirtle."
                }
            } catch {
                DispatchQueue.main.async { [weak self] in
                    guard let self else { return }
                    self.modelSelectionState.finishLoading(model, succeeded: false)
                    self.selectedModel = self.modelSelectionState.active
                    self.isSwitchingModel = self.modelSelectionState.isSwitching
                    self.statusMessage = "The \(model.displayName) model could not be loaded: \(error.localizedDescription)"
                }
            }
        }
    }

    private func configureAndStart() {
        sessionQueue.async { [weak self] in
            guard let self else { return }
            guard !self.session.isRunning else { return }

            self.session.beginConfiguration()
            self.session.sessionPreset = .hd1280x720

            guard let camera = CameraDeviceSelector.backCamera(),
                  let input = try? AVCaptureDeviceInput(device: camera),
                  self.session.canAddInput(input) else {
                self.session.commitConfiguration()
                self.publish(status: "A rear camera is not available on this device.")
                return
            }
            self.session.addInput(input)

            do {
                try camera.lockForConfiguration()
                if camera.isFocusModeSupported(.continuousAutoFocus) {
                    camera.focusMode = .continuousAutoFocus
                }
                if camera.isExposureModeSupported(.continuousAutoExposure) {
                    camera.exposureMode = .continuousAutoExposure
                }
                camera.isSubjectAreaChangeMonitoringEnabled = true
                camera.unlockForConfiguration()
                self.cameraDevice = camera
                let deviceMaximum = min(3, camera.activeFormat.videoMaxZoomFactor)
                DispatchQueue.main.async { [weak self] in
                    self?.maximumZoom = max(1, deviceMaximum)
                    self?.zoomFactor = 1
                }
            } catch {
                self.session.commitConfiguration()
                self.publish(status: "The rear camera could not be focused: \(error.localizedDescription)")
                return
            }

            let output = AVCaptureVideoDataOutput()
            output.alwaysDiscardsLateVideoFrames = true
            output.setSampleBufferDelegate(self, queue: self.outputQueue)
            guard self.session.canAddOutput(output) else {
                self.session.commitConfiguration()
                self.publish(status: "The camera output could not be configured.")
                return
            }
            self.session.addOutput(output)
            output.connection(with: .video)?.videoRotationAngle = self.rotationAngle
            self.videoOutput = output
            self.session.commitConfiguration()

            self.session.startRunning()

            let initialModel = DetectorModel.defaultSelection
            self.outputQueue.async { [weak self] in
                guard let self else { return }
                do {
                    let initialDetector = try PokemonDetector(model: initialModel)
                    self.detectorCache[initialModel] = initialDetector
                    self.detector = initialDetector
                    DispatchQueue.main.async { [weak self] in
                        guard let self else { return }
                        self.modelSelectionState.finishLoading(initialModel, succeeded: true)
                        self.selectedModel = self.modelSelectionState.active
                        self.isSwitchingModel = self.modelSelectionState.isSwitching
                        self.statusMessage = "Using \(initialModel.displayName). Point the camera at Pikachu, Charmander, or Squirtle."
                    }
                } catch {
                    DispatchQueue.main.async { [weak self] in
                        guard let self else { return }
                        self.modelSelectionState.finishLoading(initialModel, succeeded: false)
                        self.selectedModel = self.modelSelectionState.active
                        self.isSwitchingModel = self.modelSelectionState.isSwitching
                        self.statusMessage = "The Pokémon model could not be loaded: \(error.localizedDescription)"
                    }
                }
            }
        }
    }

    private func showPermissionDenied() {
        statusMessage = "Camera access is required. Enable it in Settings to identify Pokémon."
    }

    private func publish(status: String) {
        DispatchQueue.main.async { [weak self] in
            self?.statusMessage = status
        }
    }
}

extension CameraManager: AVCaptureVideoDataOutputSampleBufferDelegate {
    nonisolated func captureOutput(
        _ output: AVCaptureOutput,
        didOutput sampleBuffer: CMSampleBuffer,
        from connection: AVCaptureConnection
    ) {
        guard let pixelBuffer = CMSampleBufferGetImageBuffer(sampleBuffer) else { return }
        let frame = SendableCameraFrame(pixelBuffer: pixelBuffer)
        let sourceAspectRatio = CGFloat(CVPixelBufferGetWidth(pixelBuffer)) / CGFloat(CVPixelBufferGetHeight(pixelBuffer))
        DispatchQueue.main.async { [weak self] in
            self?.sourceAspectRatio = sourceAspectRatio
        }
        outputQueue.async { [weak self] in
            guard let self, !self.isInferenceRunning, let detector = self.detector else { return }
            self.isInferenceRunning = true
            defer { self.isInferenceRunning = false }

            do {
                let detections = try detector.detect(in: frame.pixelBuffer)
                let inferenceFPS = self.inferenceFPSCounter.recordCompletion(
                    at: ProcessInfo.processInfo.systemUptime
                )
                DispatchQueue.main.async { [weak self] in
                    self?.detections = detections
                    self?.inferenceFPS = inferenceFPS
                }
            } catch {
                self.publish(status: "Detection failed: \(error.localizedDescription)")
            }
        }
    }
}
