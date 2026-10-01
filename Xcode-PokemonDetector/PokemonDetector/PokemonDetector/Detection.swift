import Foundation

struct Detection: Identifiable, Equatable {
    let id = UUID()
    let rect: CGRect
    let label: String
    let confidence: Float
}
