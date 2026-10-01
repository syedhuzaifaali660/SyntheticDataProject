import Foundation

struct InferenceFPSCounter {
    private let smoothingFactor: Double
    private var previousCompletionTime: TimeInterval?
    private(set) var framesPerSecond: Double = 0

    init(smoothingFactor: Double = 0.2) {
        self.smoothingFactor = smoothingFactor
    }

    mutating func recordCompletion(at timestamp: TimeInterval) -> Double {
        guard let previousCompletionTime, timestamp > previousCompletionTime else {
            if self.previousCompletionTime == nil {
                self.previousCompletionTime = timestamp
            }
            return framesPerSecond
        }

        let instantaneousFPS = 1 / (timestamp - previousCompletionTime)
        framesPerSecond = framesPerSecond == 0
            ? instantaneousFPS
            : smoothingFactor * instantaneousFPS + (1 - smoothingFactor) * framesPerSecond
        self.previousCompletionTime = timestamp
        return framesPerSecond
    }
}
