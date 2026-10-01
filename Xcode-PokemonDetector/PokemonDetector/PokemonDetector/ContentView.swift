//
//  ContentView.swift
//  PokemonDetector
//
//  Created by Syed Huzaifa Ali on 03/09/2026.
//

import SwiftUI

struct ContentView: View {
    @StateObject private var camera = CameraManager()

    var body: some View {
        GeometryReader { geometry in
            ZStack(alignment: .bottom) {
                CameraPreview(session: camera.session, rotationAngle: camera.rotationAngle)
                    .ignoresSafeArea()

                DetectionOverlay(detections: camera.detections, sourceAspectRatio: camera.sourceAspectRatio)

                Text(String(
                    format: "FPS %.1f • %@",
                    camera.inferenceFPS,
                    camera.selectedModel?.shortName ?? "Loading"
                ))
                    .font(.system(size: 22, weight: .semibold, design: .monospaced))
                    .foregroundStyle(.white)
                    .padding(.horizontal, 12)
                    .padding(.vertical, 8)
                    .background(.black.opacity(0.65), in: RoundedRectangle(cornerRadius: 10))
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
                    .padding(.leading, 16)
                    .padding(.top, geometry.safeAreaInsets.top + 12)

                VStack(spacing: 10) {
                    HStack(spacing: 8) {
                        ForEach(DetectorModel.allCases, id: \.self) { model in
                            Button {
                                camera.selectModel(model)
                            } label: {
                                Text(model.displayName)
                                    .font(.subheadline.weight(.semibold))
                                    .lineLimit(1)
                                    .frame(maxWidth: .infinity)
                                    .padding(.horizontal, 10)
                                    .padding(.vertical, 10)
                                    .foregroundStyle(camera.selectedModel == model ? .black : .white)
                                    .background(
                                        camera.selectedModel == model ? Color.green : Color.white.opacity(0.16),
                                        in: RoundedRectangle(cornerRadius: 10)
                                    )
                            }
                            .buttonStyle(.plain)
                            .disabled(camera.isSwitchingModel)
                        }
                    }
                    .padding(6)
                    .background(.black.opacity(0.65), in: RoundedRectangle(cornerRadius: 14))

                    HStack {
                        Image(systemName: "minus.magnifyingglass")
                        if CameraZoom.isAdjustable(maximum: camera.maximumZoom) {
                            Slider(
                                value: Binding(
                                    get: { Double(camera.zoomFactor) },
                                    set: { camera.updateZoom(CGFloat($0)) }
                                ),
                                in: 1...Double(camera.maximumZoom),
                                step: 0.1
                            )
                        } else {
                            Text("Zoom unavailable")
                                .font(.caption)
                        }
                        Image(systemName: "plus.magnifyingglass")
                        Text(String(format: "%.1fx", camera.zoomFactor))
                            .monospacedDigit()
                    }
                    .foregroundStyle(.white)
                    .padding(12)
                    .background(.black.opacity(0.65), in: Capsule())

                    Text(camera.statusMessage)
                        .font(.footnote)
                        .foregroundStyle(.white)
                        .multilineTextAlignment(.center)
                        .padding(12)
                        .background(.black.opacity(0.65), in: Capsule())
                }
                .padding()
            }
            .onAppear { camera.updateLayout(width: geometry.size.width, height: geometry.size.height) }
            .onChange(of: geometry.size) { _, size in
                camera.updateLayout(width: size.width, height: size.height)
            }
        }
        .onAppear { camera.start() }
        .onDisappear { camera.stop() }
    }
}

#Preview {
    ContentView()
}
