import AppKit

@main
final class AppDelegate: NSObject, NSApplicationDelegate {
    private var pet: MochiPanel!
    private var statusItem: NSStatusItem!

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        pet = MochiPanel()
        pet.orderFrontRegardless()
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        statusItem.button?.title = "🐾"
        let menu = NSMenu()
        let title = NSMenuItem(title: "🐾  麻薯  ·  Mochi", action: nil, keyEquivalent: "")
        title.isEnabled = false
        menu.addItem(title)
        let status = NSMenuItem(title: "   当前状态：自在发呆", action: nil, keyEquivalent: "")
        status.isEnabled = false
        menu.addItem(status)
        menu.addItem(.separator())
        let normal = NSMenuItem(title: "✨  普通模式", action: #selector(normalMode), keyEquivalent: "")
        normal.state = .on
        menu.addItem(normal)
        menu.addItem(withTitle: "🌙  安静模式", action: #selector(quietMode), keyEquivalent: "")
        let pets = NSMenuItem(title: "🐱  切换桌宠", action: nil, keyEquivalent: "")
        let petMenu = NSMenu(title: "切换桌宠")
        petMenu.addItem(withTitle: "🐾  麻薯", action: #selector(selectMochi), keyEquivalent: "")
        petMenu.addItem(withTitle: "🐾  年糕", action: #selector(selectNiangao), keyEquivalent: "")
        pets.submenu = petMenu
        menu.addItem(pets)
        let actions = NSMenuItem(title: "✦  做个动作", action: nil, keyEquivalent: "")
        let actionMenu = NSMenu(title: "做个动作")
        actionMenu.addItem(withTitle: "👀  侧头看看", action: #selector(sideLook), keyEquivalent: "")
        actionMenu.addItem(withTitle: "☁  躺一会", action: #selector(sideLie), keyEquivalent: "")
        actionMenu.addItem(withTitle: "✦  舔舔爪", action: #selector(groom), keyEquivalent: "")
        actions.submenu = actionMenu
        menu.addItem(actions)
        menu.addItem(.separator())
        menu.addItem(withTitle: "退出 Mochi", action: #selector(quit), keyEquivalent: "q")
        statusItem.menu = menu
    }

    @objc private func normalMode() { pet.quiet = false }
    @objc private func quietMode() { pet.quiet = true }
    @objc private func sideLook() { pet.sideLook() }
    @objc private func sideLie() { pet.sideLie() }
    @objc private func groom() { pet.groom() }
    @objc private func selectMochi() { pet.petName = "mochi" }
    @objc private func selectNiangao() { pet.petName = "niangao" }
    @objc private func quit() { NSApp.terminate(nil) }
}

final class MochiPanel: NSPanel {
    var quiet = false { didSet { view.petView.quiet = quiet } }
    var petName = "mochi" { didSet { view.petName = petName } }
    private var dragOffset = NSPoint.zero
    private var view: PetHostView { contentView as! PetHostView }

    init() {
        let screen = NSScreen.main?.visibleFrame ?? .zero
        super.init(contentRect: NSRect(x: screen.maxX - 230, y: screen.minY + 35, width: 200, height: 135),
                   styleMask: .borderless, backing: .buffered, defer: false)
        isOpaque = false
        backgroundColor = .clear
        hasShadow = false
        level = .floating
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        contentView = PetHostView(frame: NSRect(x: 0, y: 0, width: 200, height: 135))
    }

    override var canBecomeKey: Bool { false }
    override func mouseDown(with event: NSEvent) { dragOffset = event.locationInWindow }
    override func mouseDragged(with event: NSEvent) {
        let screen = event.locationInScreen
        setFrameOrigin(NSPoint(x: screen.x - dragOffset.x, y: screen.y - dragOffset.y))
    }
    override func rightMouseDown(with event: NSEvent) {
        let menu = NSMenu()
        let title = NSMenuItem(title: "🐾  麻薯  ·  Mochi", action: nil, keyEquivalent: "")
        title.isEnabled = false
        menu.addItem(title)
        let status = NSMenuItem(title: quiet ? "   当前状态：安静休息" : "   当前状态：自在发呆", action: nil, keyEquivalent: "")
        status.isEnabled = false
        menu.addItem(status)
        menu.addItem(.separator())
        let normal = NSMenuItem(title: "✨  普通模式", action: #selector(setNormal), keyEquivalent: "")
        normal.state = quiet ? .off : .on
        menu.addItem(normal)
        let quietItem = NSMenuItem(title: "🌙  安静模式", action: #selector(setQuiet), keyEquivalent: "")
        quietItem.state = quiet ? .on : .off
        menu.addItem(quietItem)
        let pets = NSMenuItem(title: "🐱  切换桌宠", action: nil, keyEquivalent: "")
        let petMenu = NSMenu(title: "切换桌宠")
        petMenu.addItem(withTitle: "🐾  麻薯", action: #selector(selectMochi), keyEquivalent: "")
        petMenu.addItem(withTitle: "🐾  年糕", action: #selector(selectNiangao), keyEquivalent: "")
        pets.submenu = petMenu
        menu.addItem(pets)
        let actions = NSMenuItem(title: "✦  做个动作", action: nil, keyEquivalent: "")
        let actionMenu = NSMenu(title: "做个动作")
        actionMenu.addItem(withTitle: "👀  侧头看看", action: #selector(sideLook), keyEquivalent: "")
        actionMenu.addItem(withTitle: "☁  躺一会", action: #selector(sideLie), keyEquivalent: "")
        actionMenu.addItem(withTitle: "✦  舔舔爪", action: #selector(groom), keyEquivalent: "")
        actions.submenu = actionMenu
        menu.addItem(actions)
        menu.addItem(.separator())
        menu.addItem(withTitle: "退出", action: #selector(quit), keyEquivalent: "")
        NSMenu.popUpContextMenu(menu, with: event, for: view)
    }
    @objc private func setNormal() { quiet = false }
    @objc private func setQuiet() { quiet = true }
    @objc private func sideLook() { view.sideLook() }
    @objc private func sideLie() { view.sideLie() }
    @objc private func groom() { view.groom() }
    @objc private func selectMochi() { petName = "mochi" }
    @objc private func selectNiangao() { petName = "niangao" }
    @objc private func quit() { NSApp.terminate(nil) }
}

final class PetHostView: NSView {
    private enum MicroAction { case none, sideLook, sideLie, groom }
    private enum Timing {
        static let idleTick = 0.1
        static let walkFrame = 0.11
        static let groomFrame = 0.22
        static let blinkDuration = 0.24
        static let blinkHalfPhase = 0.06
        static func walkDelay() -> Double { Double.random(in: 40...80) }
        static func actionDelay() -> Double { Double.random(in: 18...35) }
        static func blinkDelay() -> Double { Double.random(in: 7...12) }
    }
    var quiet = false {
        didSet {
            if quiet {
                walking = false
                action = .none
                blinkUntil = 0
            }
            let now = ProcessInfo.processInfo.systemUptime
            nextWalk = now + Timing.walkDelay()
            nextAction = now + Timing.actionDelay()
            needsDisplay = true
        }
    }
    var petName = "mochi" {
        didSet {
            petCache.removeAll()
            needsDisplay = true
        }
    }
    private var petCache: [String: PetImages] = [:]
    private var pet: PetImages {
        if let cached = petCache[petName] { return cached }
        let loaded = PetImages(petName)
        petCache[petName] = loaded
        return loaded
    }
    private var walking = false
    private var walkEnds = 0.0
    private var blinkStarted = 0.0
    private var blinkUntil = 0.0
    private var nextBlink = ProcessInfo.processInfo.systemUptime + 5
    private var nextWalk = ProcessInfo.processInfo.systemUptime + 45
    private var walkFrame = 0
    private var action = MicroAction.none
    private var actionEnds = 0.0
    private var nextAction = ProcessInfo.processInfo.systemUptime + 20
    private var groomFrame = 0
    private var nextGroomFrame = 0.0
    private var nextWalkFrame = 0.0

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        Timer.scheduledTimer(withTimeInterval: Timing.idleTick, repeats: true) { [weak self] _ in self?.tick() }
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }

    private func tick() {
        let now = ProcessInfo.processInfo.systemUptime
        var changed = false
        if !quiet && !walking && now >= nextWalk {
            walking = true
            walkFrame = 0
            nextWalkFrame = now + Timing.walkFrame
            walkEnds = now + 1.4
            changed = true
        }
        if walking && now >= nextWalkFrame {
            walkFrame = (walkFrame + 1) % pet.walk.count
            nextWalkFrame = now + Timing.walkFrame
            changed = true
        }
        if walking && now >= walkEnds {
            walking = false
            walkFrame = 0
            nextWalk = now + Timing.walkDelay()
            if nextAction <= now { nextAction = now + Timing.actionDelay() }
            if nextBlink <= now { nextBlink = now + Timing.blinkDelay() }
            changed = true
        }
        if !quiet && !walking && action == .none && now >= nextAction {
            action = [.sideLook, .sideLie, .groom].randomElement()!
            actionEnds = now + (action == .sideLook ? 3 : action == .sideLie ? 7 : 5)
            groomFrame = 0
            nextGroomFrame = now + Timing.groomFrame
            changed = true
        }
        if action != .none && now >= actionEnds {
            action = .none
            nextAction = now + Timing.actionDelay()
            if nextWalk <= now { nextWalk = now + Timing.walkDelay() }
            if nextBlink <= now { nextBlink = now + Timing.blinkDelay() }
            changed = true
        }
        if action == .groom && now >= nextGroomFrame {
            groomFrame = (groomFrame + 1) % pet.groom.count
            nextGroomFrame = now + Timing.groomFrame
            changed = true
        }
        if !quiet && !walking && action == .none && now >= nextBlink && now >= blinkUntil {
            blinkStarted = now
            blinkUntil = now + Timing.blinkDuration
            nextBlink = now + Timing.blinkDelay()
            changed = true
        }
        if walking || now < blinkUntil { changed = true }
        if changed { needsDisplay = true }
    }

    func sideLook() { start(.sideLook) }
    func sideLie() { start(.sideLie) }
    func groom() { start(.groom) }

    private func start(_ next: MicroAction) {
        guard !quiet else { return }
        let now = ProcessInfo.processInfo.systemUptime
        if walking { nextWalk = now + Timing.walkDelay() }
        walking = false
        action = next
        blinkUntil = 0
        actionEnds = now + (next == .sideLook ? 3 : next == .sideLie ? 7 : 5)
        groomFrame = 0
        nextGroomFrame = now + Timing.groomFrame
        needsDisplay = true
    }

    override func draw(_ dirtyRect: NSRect) {
        NSColor.clear.setFill(); dirtyRect.fill()
        if walking {
            pet.walk[walkFrame % pet.walk.count].draw(in: pet.walkBounds)
        } else if action == .groom {
            pet.groom[groomFrame % pet.groom.count].draw(in: pet.groomBounds)
        } else {
            let pose = action == .sideLook ? pet.sideLook : action == .sideLie ? pet.sideLie : pet.rest
            pose.draw(in: pet.poseBounds)
            let now = ProcessInfo.processInfo.systemUptime
            if action == .none && now < blinkUntil {
                let elapsed = now - blinkStarted
                let remaining = blinkUntil - now
                let blink = elapsed < Timing.blinkHalfPhase || remaining < Timing.blinkHalfPhase ? pet.blinkHalf : pet.blink
                NSGraphicsContext.saveGraphicsState()
                let eyePath = NSBezierPath()
                pet.blinkEyeClips.forEach { eyePath.appendOval(in: $0) }
                eyePath.addClip()
                blink.draw(in: pet.poseBounds)
                NSGraphicsContext.restoreGraphicsState()
            }
        }
    }
}

private struct PetFrame {
    let image: NSImage
    let source: NSRect
    func draw(in destination: NSRect) {
        image.draw(in: destination, from: source, operation: .sourceOver, fraction: 1)
    }
}

private final class PetImages {
    let rest: NSImage
    let blinkHalf: NSImage
    let blink: NSImage
    let walk: [PetFrame]
    let sideLook: NSImage
    let sideLie: NSImage
    let groom: [PetFrame]
    let poseBounds = NSRect(x: 10, y: 10, width: 180, height: 120)
    let walkBounds: NSRect
    let groomBounds: NSRect
    let blinkEyeClips: [NSRect]

    init(_ name: String) {
        func image(_ action: String) -> NSImage {
            NSImage(contentsOfFile: Bundle.main.path(forResource: "\(name)-\(action)", ofType: "png", inDirectory: "assets/cats")!)!
        }
        rest = image("rest")
        blink = image("blink")
        sideLook = image("side-look")
        sideLie = image("side-lie")
        if name == "niangao" {
            blinkHalf = image("blink-half")
            walk = (1...4).map { index in
                let frame = image(String(format: "walk-%02d", index))
                return PetFrame(image: frame, source: NSRect(origin: .zero, size: frame.size))
            }
            groom = (1...3).map { index in
                let frame = image(String(format: "groom-%02d", index))
                return PetFrame(image: frame, source: NSRect(origin: .zero, size: frame.size))
            }
            walkBounds = NSRect(x: 10, y: 10, width: 180, height: 120)
            groomBounds = NSRect(x: 10, y: 10, width: 180, height: 120)
            blinkEyeClips = [NSRect(x: 32, y: 62, width: 16, height: 13), NSRect(x: 55, y: 62, width: 17, height: 13)]
        } else {
            blinkHalf = blink
            let walkSheet = image("walk")
            let walkWidth = walkSheet.size.width / 4
            walk = (0..<4).map { PetFrame(image: walkSheet, source: NSRect(x: CGFloat($0) * walkWidth, y: 120, width: walkWidth, height: 430)) }
            let groomSheet = image("groom")
            let groomWidth = groomSheet.size.width / 3
            groom = (0..<3).map { PetFrame(image: groomSheet, source: NSRect(x: CGFloat($0) * groomWidth, y: 100, width: groomWidth, height: 540)) }
            walkBounds = NSRect(x: 17, y: 4, width: 165, height: 131)
            groomBounds = NSRect(x: 32, y: 28, width: 135, height: 100)
            blinkEyeClips = [NSRect(x: 46, y: 69, width: 14, height: 12), NSRect(x: 69, y: 67, width: 14, height: 12)]
        }
    }
}
