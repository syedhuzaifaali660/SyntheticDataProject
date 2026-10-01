import Foundation

@main
struct InferenceFPSCounterTests {
    static func main() {
        var counter = InferenceFPSCounter(smoothingFactor: 0.25)

        precondition(counter.recordCompletion(at: 10) == 0)
        precondition(counter.recordCompletion(at: 10.25) == 4)
        precondition(counter.recordCompletion(at: 10.75) == 3.5)
        precondition(counter.recordCompletion(at: 10.75) == 3.5)

        print("InferenceFPSCounterTests passed")
    }
}
