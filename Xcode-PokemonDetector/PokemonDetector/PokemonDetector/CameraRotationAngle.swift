import CoreGraphics

enum CameraRotationAngle {
    static func forLayout(width: CGFloat, height: CGFloat) -> CGFloat {
        width > height ? 0 : 90
    }
}
