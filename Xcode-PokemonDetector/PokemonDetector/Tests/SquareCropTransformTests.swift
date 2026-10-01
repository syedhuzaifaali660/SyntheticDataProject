import CoreGraphics

@main
struct SquareCropTransformTests {
    static func main() {
        testUltralyticsPaddingColor()
        testPortraitLetterbox()
        testLandscapeLetterbox()
        print("SquareCropTransformTests passed")
    }

    private static func testUltralyticsPaddingColor() {
        precondition(abs(SquareCropTransform.paddingComponent - (114.0 / 255.0)) < 0.0001)
    }

    private static func testPortraitLetterbox() {
        let portrait = SquareCropTransform(sourceSize: CGSize(width: 720, height: 1280))
        let rect = portrait.modelRectToSource(CGRect(x: 0.25, y: 0.25, width: 0.25, height: 0.25))

        precondition(abs(rect.minX - 0.0555556) < 0.0001)
        precondition(abs(rect.minY - 0.25) < 0.0001)
        precondition(abs(rect.width - 0.4444444) < 0.0001)
        precondition(abs(rect.height - 0.25) < 0.0001)
    }

    private static func testLandscapeLetterbox() {
        let landscape = SquareCropTransform(sourceSize: CGSize(width: 1280, height: 720))
        let rect = landscape.modelRectToSource(CGRect(x: 0.25, y: 0.25, width: 0.25, height: 0.25))

        precondition(abs(rect.minX - 0.25) < 0.0001)
        precondition(abs(rect.minY - 0.0555556) < 0.0001)
        precondition(abs(rect.width - 0.25) < 0.0001)
        precondition(abs(rect.height - 0.4444444) < 0.0001)
    }
}
