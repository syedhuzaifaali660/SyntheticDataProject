import CoreGraphics

@main
struct PokemonOutputParserTests {
    static func main() {
        testKnownClassAndNormalizedBox()
        testFiltersInvalidRows()
        print("PokemonOutputParserTests passed")
    }

    private static func testKnownClassAndNormalizedBox() {
        let detections = PokemonOutputParser.parse(rows: [[64, 128, 320, 448, 0.90, 0]])

        precondition(detections.count == 1)
        precondition(detections[0].label == "Pikachu")
        let expected = CGRect(x: 0.1, y: 0.2, width: 0.4, height: 0.5)
        precondition(abs(detections[0].rect.minX - expected.minX) < 0.0001)
        precondition(abs(detections[0].rect.minY - expected.minY) < 0.0001)
        precondition(abs(detections[0].rect.width - expected.width) < 0.0001)
        precondition(abs(detections[0].rect.height - expected.height) < 0.0001)
    }

    private static func testFiltersInvalidRows() {
        let detections = PokemonOutputParser.parse(rows: [
            [0, 0, 640, 640, 0.49, 1],
            [0, 0, 640, 640, 0.95, 9],
            [500, 100, 100, 500, 0.95, 2],
        ])

        precondition(detections.isEmpty)
    }
}
