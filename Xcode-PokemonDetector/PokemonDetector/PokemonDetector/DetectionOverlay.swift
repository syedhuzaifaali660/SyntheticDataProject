import SwiftUI

struct DetectionOverlay: View {
    let detections: [Detection]
    let sourceAspectRatio: CGFloat

    var body: some View {
        GeometryReader { geometry in
            ForEach(detections) { detection in
                let sourceSize = CGSize(width: sourceAspectRatio, height: 1)
                let scale = max(geometry.size.width / sourceSize.width, geometry.size.height / sourceSize.height)
                let displayedSize = CGSize(width: sourceSize.width * scale, height: sourceSize.height * scale)
                let offset = CGPoint(x: (geometry.size.width - displayedSize.width) / 2, y: (geometry.size.height - displayedSize.height) / 2)
                let rect = CGRect(x: offset.x + detection.rect.minX * displayedSize.width, y: offset.y + detection.rect.minY * displayedSize.height, width: detection.rect.width * displayedSize.width, height: detection.rect.height * displayedSize.height)
                RoundedRectangle(cornerRadius: 8)
                    .stroke(.green, lineWidth: 3)
                    .frame(width: rect.width, height: rect.height)
                    .position(x: rect.midX, y: rect.midY)
                    .overlay(alignment: .topLeading) {
                        Text("\(detection.label) \(Int(detection.confidence * 100))%")
                            .font(.caption.bold())
                            .foregroundStyle(.black)
                            .padding(5)
                            .background(.green, in: RoundedRectangle(cornerRadius: 5))
                            .offset(x: rect.minX, y: rect.minY - 28)
                    }
            }
        }
        .allowsHitTesting(false)
    }
}
