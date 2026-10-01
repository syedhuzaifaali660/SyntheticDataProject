import Foundation

@main
struct CameraZoomTests {
    static func main() {
        precondition(CameraZoom.clamp(0.5, maximum: 3) == 1)
        precondition(CameraZoom.clamp(2, maximum: 3) == 2)
        precondition(CameraZoom.clamp(8, maximum: 3) == 3)
        precondition(!CameraZoom.isAdjustable(maximum: 1))
        precondition(CameraZoom.isAdjustable(maximum: 1.1))
        print("CameraZoomTests passed")
    }
}
