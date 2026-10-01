import CoreGraphics

struct SquareCropTransform {
    static let paddingComponent: CGFloat = 114.0 / 255.0

    let sourceSize: CGSize
    let scale: CGFloat
    let padX: CGFloat
    let padY: CGFloat

    init(sourceSize: CGSize) {
        self.sourceSize = sourceSize
        scale = min(640 / sourceSize.width, 640 / sourceSize.height)
        padX = (640 - sourceSize.width * scale) / 2
        padY = (640 - sourceSize.height * scale) / 2
    }

    func modelRectToSource(_ rect: CGRect) -> CGRect {
        CGRect(
            x: (rect.minX * 640 - padX) / (sourceSize.width * scale),
            y: (rect.minY * 640 - padY) / (sourceSize.height * scale),
            width: rect.width * 640 / (sourceSize.width * scale),
            height: rect.height * 640 / (sourceSize.height * scale)
        )
    }
}
