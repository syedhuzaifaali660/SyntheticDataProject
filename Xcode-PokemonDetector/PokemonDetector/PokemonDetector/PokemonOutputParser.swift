import CoreGraphics

enum PokemonOutputParser {
    static let inputDimension: Float = 640
    static let labels = [0: "Pikachu", 1: "Charmander", 2: "Squirtle"]

    static func parse(rows: [[Float]], confidenceThreshold: Float = 0.50) -> [Detection] {
        rows.compactMap { row in
            guard row.count == 6,
                  row[4] >= confidenceThreshold,
                  let label = labels[Int(row[5])],
                  row[2] > row[0],
                  row[3] > row[1] else {
                return nil
            }

            let rect = CGRect(
                x: CGFloat(row[0] / inputDimension),
                y: CGFloat(row[1] / inputDimension),
                width: CGFloat((row[2] - row[0]) / inputDimension),
                height: CGFloat((row[3] - row[1]) / inputDimension)
            )

            return Detection(rect: rect, label: label, confidence: row[4])
        }
    }
}
