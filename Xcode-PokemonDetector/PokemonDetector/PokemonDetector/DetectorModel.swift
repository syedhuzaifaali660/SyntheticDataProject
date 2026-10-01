import Foundation

enum DetectorModel: String, CaseIterable, Hashable {
    case baseline3900
    case mixedDevice6000

    static let defaultSelection: DetectorModel = .mixedDevice6000

    var displayName: String {
        switch self {
        case .baseline3900:
            "Baseline 3900"
        case .mixedDevice6000:
            "Mixed Device 6000"
        }
    }

    var shortName: String {
        switch self {
        case .baseline3900:
            "Baseline"
        case .mixedDevice6000:
            "Mixed 6000"
        }
    }

    var resourceName: String {
        switch self {
        case .baseline3900:
            "best"
        case .mixedDevice6000:
            "MixedDevice6000"
        }
    }
}
