struct DetectorSelectionState {
    private(set) var active: DetectorModel?
    private(set) var loading: DetectorModel?

    static let initial = DetectorSelectionState(
        active: nil,
        loading: DetectorModel.defaultSelection
    )

    var isSwitching: Bool {
        loading != nil
    }

    mutating func beginLoading(_ model: DetectorModel) -> Bool {
        guard loading == nil, active != model else { return false }
        loading = model
        return true
    }

    mutating func finishLoading(_ model: DetectorModel, succeeded: Bool) {
        guard loading == model else { return }
        if succeeded {
            active = model
        }
        loading = nil
    }
}
