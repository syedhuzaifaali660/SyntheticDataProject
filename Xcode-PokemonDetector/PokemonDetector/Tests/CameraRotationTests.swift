import Foundation

@main
struct CameraRotationTests {
    static func main() {
        precondition(CameraRotationAngle.forLayout(width: 390, height: 844) == 90)
        precondition(CameraRotationAngle.forLayout(width: 844, height: 390) == 0)
        print("CameraRotationTests passed")
    }
}
