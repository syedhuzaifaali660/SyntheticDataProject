import CoreGraphics

enum CameraZoom {
    static func isAdjustable(maximum: CGFloat) -> Bool {
        maximum > 1
    }

    static func clamp(_ value: CGFloat, maximum: CGFloat) -> CGFloat {
        min(max(1, value), max(1, maximum))
    }
}
