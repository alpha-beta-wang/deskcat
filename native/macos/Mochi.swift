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
    var quiet = false
    var petName = "mochi" { didSet { needsDisplay = true } }
    private let mochi = PetImages("mochi")
    private let niangao = PetImages("niangao")
    private var pet: PetImages { petName == "niangao" ? niangao : mochi }
    private var walking = false
    private var blinkUntil = Date.distantPast
    private var nextBlink = Date().addingTimeInterval(5)
    private var nextWalk = Date().addingTimeInterval(45)
    private var walkFrame = 0
    private var action = MicroAction.none
    private var actionEnds = Date.distantPast
    private var nextAction = Date().addingTimeInterval(20)
    private var groomFrame = 0
    private var nextGroomFrame = Date.distantPast

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        Timer.scheduledTimer(withTimeInterval: 0.1, repeats: true) { [weak self] _ in self?.tick() }
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }

    private func tick() {
        let now = Date()
        var changed = false
        if !quiet && !walking && now >= nextWalk {
            walking = true
            walkFrame = 0
            nextWalk = now.addingTimeInterval(TimeInterval(Int.random(in: 40...80)))
            DispatchQueue.main.asyncAfter(deadline: .now() + 1.4) { [weak self] in self?.walking = false }
            changed = true
        }
        if !quiet && !walking && action == .none && now >= nextAction {
            action = [.sideLook, .sideLie, .groom].randomElement()!
            actionEnds = now.addingTimeInterval(action == .sideLook ? 3 : action == .sideLie ? 7 : 5)
            groomFrame = 0
            nextGroomFrame = now
            changed = true
        }
        if action != .none && now >= actionEnds {
            action = .none
            nextAction = now.addingTimeInterval(TimeInterval(Int.random(in: 18...35)))
            changed = true
        }
        if action == .groom && now >= nextGroomFrame {
            groomFrame = (groomFrame + 1) % 3
            nextGroomFrame = now.addingTimeInterval(0.22)
            changed = true
        }
        if !walking && action == .none && now >= nextBlink {
            blinkUntil = now.addingTimeInterval(0.22)
            nextBlink = now.addingTimeInterval(TimeInterval(Int.random(in: 7...12)))
            changed = true
        }
        if walking || Date() < blinkUntil { changed = true }
        if changed { needsDisplay = true }
    }

    func sideLook() { start(.sideLook) }
    func sideLie() { start(.sideLie) }
    func groom() { start(.groom) }

    private func start(_ next: MicroAction) {
        guard !quiet else { return }
        let now = Date()
        walking = false
        action = next
        actionEnds = now.addingTimeInterval(next == .sideLook ? 3 : next == .sideLie ? 7 : 5)
        groomFrame = 0
        nextGroomFrame = now
        needsDisplay = true
    }

    override func draw(_ dirtyRect: NSRect) {
        NSColor.clear.setFill(); dirtyRect.fill()
        if walking {
            walkFrame = (walkFrame + 1) % 4
            let frameWidth = pet.walk.size.width / 4
            let sourceY: CGFloat = petName == "niangao" ? 0 : 120
            let sourceHeight: CGFloat = petName == "niangao" ? pet.walk.size.height : 430
            pet.walk.draw(in: NSRect(x: 17, y: 4, width: 165, height: 131), from: NSRect(x: CGFloat(walkFrame) * frameWidth, y: sourceY, width: frameWidth, height: sourceHeight), operation: .sourceOver, fraction: 1)
        } else if action == .groom {
            let frameWidth = pet.groom.size.width / 3
            let sourceY: CGFloat = petName == "niangao" ? 0 : 100
            let sourceHeight: CGFloat = petName == "niangao" ? pet.groom.size.height : 540
            pet.groom.draw(in: NSRect(x: 32, y: 28, width: 135, height: 100), from: NSRect(x: CGFloat(groomFrame) * frameWidth, y: sourceY, width: frameWidth, height: sourceHeight), operation: .sourceOver, fraction: 1)
        } else {
            let pose = action == .sideLook ? pet.sideLook : action == .sideLie ? pet.sideLie : Date() < blinkUntil ? pet.blink : pet.rest
            pose.draw(in: NSRect(x: 10, y: 10, width: 180, height: 120), from: NSRect(x: 0, y: 0, width: pose.size.width, height: pose.size.height), operation: .sourceOver, fraction: 1)
        }
    }
}

private final class PetImages {
    let rest: NSImage
    let blink: NSImage
    let walk: NSImage
    let sideLook: NSImage
    let sideLie: NSImage
    let groom: NSImage

    init(_ name: String) {
        func image(_ action: String) -> NSImage {
            NSImage(contentsOfFile: Bundle.main.path(forResource: "\(name)-\(action)", ofType: "png", inDirectory: "assets/cats")!)!
        }
        rest = image("rest"); blink = image("blink"); walk = image("walk")
        sideLook = image("side-look"); sideLie = image("side-lie"); groom = image("groom")
    }
}
