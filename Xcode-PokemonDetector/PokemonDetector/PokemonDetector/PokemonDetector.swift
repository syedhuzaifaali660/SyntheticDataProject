import CoreML
import CoreVideo

#if canImport(UIKit)
import CoreImage
import UIKit

final class PokemonDetector {
    private let model: MLModel
    private let inputFeatureName: String
    private let outputFeatureName: String
    private let context = CIContext()

    init(model detectorModel: DetectorModel, bundle: Bundle = .main) throws {
        guard let modelURL = bundle.url(
            forResource: detectorModel.resourceName,
            withExtension: "mlmodelc"
        ) else {
            throw DetectorError.modelResourceMissing(detectorModel.displayName)
        }

        let configuration = MLModelConfiguration()
        configuration.computeUnits = .all
        model = try MLModel(contentsOf: modelURL, configuration: configuration)

        guard let inputFeatureName = model.modelDescription.inputDescriptionsByName
            .first(where: { $0.value.type == .image })?.key else {
            throw DetectorError.imageInputMissing(detectorModel.displayName)
        }
        guard let outputFeatureName = model.modelDescription.outputDescriptionsByName
            .first(where: {
                $0.value.type == .multiArray
                    && $0.value.multiArrayConstraint?.shape.map(\.intValue) == [1, 300, 6]
            })?.key else {
            throw DetectorError.detectionOutputMissing(detectorModel.displayName)
        }

        self.inputFeatureName = inputFeatureName
        self.outputFeatureName = outputFeatureName
    }

    func detect(in pixelBuffer: CVPixelBuffer) throws -> [Detection] {
        let prepared = try preparedInput(from: pixelBuffer)
        let input = try MLDictionaryFeatureProvider(dictionary: [
            inputFeatureName: MLFeatureValue(pixelBuffer: prepared.pixelBuffer)
        ])
        let prediction = try model.prediction(from: input)
        guard let output = prediction.featureValue(for: outputFeatureName)?.multiArrayValue else {
            throw DetectorError.predictionOutputMissing(outputFeatureName)
        }

        return PokemonOutputParser.parse(rows: Self.rows(from: output)).map {
            Detection(
                rect: prepared.transform.modelRectToSource($0.rect),
                label: $0.label,
                confidence: $0.confidence
            )
        }
    }

    static func rows(from output: MLMultiArray) -> [[Float]] {
        let shape = output.shape.map(\.intValue)
        let strides = output.strides.map(\.intValue)
        guard shape == [1, 300, 6], strides.count == 3 else { return [] }

        let values = output.dataPointer.bindMemory(to: Float.self, capacity: output.count)
        return (0..<shape[1]).map { row in
            (0..<shape[2]).map { column in
                values[row * strides[1] + column * strides[2]]
            }
        }
    }

    private func preparedInput(from pixelBuffer: CVPixelBuffer) throws -> (pixelBuffer: CVPixelBuffer, transform: SquareCropTransform) {
        var resized: CVPixelBuffer?
        let attributes: [CFString: Any] = [
            kCVPixelBufferCGImageCompatibilityKey: true,
            kCVPixelBufferCGBitmapContextCompatibilityKey: true,
        ]
        let result = CVPixelBufferCreate(
            kCFAllocatorDefault,
            Int(PokemonOutputParser.inputDimension),
            Int(PokemonOutputParser.inputDimension),
            kCVPixelFormatType_32BGRA,
            attributes as CFDictionary,
            &resized
        )
        guard result == kCVReturnSuccess, let resized else {
            throw DetectorError.inputBufferCreationFailed
        }

        let image = CIImage(cvPixelBuffer: pixelBuffer)
        let transform = SquareCropTransform(sourceSize: image.extent.size)
        let scaled = image.transformed(by: CGAffineTransform(scaleX: transform.scale, y: transform.scale))
        let padded = scaled.transformed(by: CGAffineTransform(translationX: transform.padX, y: transform.padY))
        let padding = SquareCropTransform.paddingComponent
        let canvas = CIImage(
            color: CIColor(red: padding, green: padding, blue: padding, alpha: 1)
        ).cropped(to: CGRect(
            x: 0,
            y: 0,
            width: CGFloat(PokemonOutputParser.inputDimension),
            height: CGFloat(PokemonOutputParser.inputDimension)
        ))
        context.render(padded.composited(over: canvas), to: resized)
        return (resized, transform)
    }

    enum DetectorError: LocalizedError {
        case modelResourceMissing(String)
        case imageInputMissing(String)
        case detectionOutputMissing(String)
        case predictionOutputMissing(String)
        case inputBufferCreationFailed

        var errorDescription: String? {
            switch self {
            case .modelResourceMissing(let model):
                "The \(model) Core ML resource is missing from the app bundle."
            case .imageInputMissing(let model):
                "The \(model) model does not contain an image input."
            case .detectionOutputMissing(let model):
                "The \(model) model does not contain the expected detection output."
            case .predictionOutputMissing(let output):
                "The model did not return its \(output) detection output."
            case .inputBufferCreationFailed:
                "The camera frame could not be prepared for the Pokémon model."
            }
        }
    }
}
#else
enum PokemonDetector {
    static func rows(from output: MLMultiArray) -> [[Float]] {
        let shape = output.shape.map(\.intValue)
        let strides = output.strides.map(\.intValue)
        guard shape == [1, 300, 6], strides.count == 3 else { return [] }

        let values = output.dataPointer.bindMemory(to: Float.self, capacity: output.count)
        return (0..<shape[1]).map { row in
            (0..<shape[2]).map { column in
                values[row * strides[1] + column * strides[2]]
            }
        }
    }
}
#endif
