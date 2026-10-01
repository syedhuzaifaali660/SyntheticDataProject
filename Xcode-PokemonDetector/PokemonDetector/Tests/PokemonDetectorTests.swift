import CoreML

@main
struct PokemonDetectorTests {
    static func main() throws {
        let array = try MLMultiArray(shape: [1, 300, 6], dataType: .float32)
        array[[0, 0, 0] as [NSNumber]] = 64
        array[[0, 0, 4] as [NSNumber]] = 0.9
        array[[0, 0, 5] as [NSNumber]] = 2

        let rows = PokemonDetector.rows(from: array)
        precondition(rows.count == 300)
        precondition(rows[0] == [64, 0, 0, 0, 0.9, 2])
        print("PokemonDetectorTests passed")
    }
}
