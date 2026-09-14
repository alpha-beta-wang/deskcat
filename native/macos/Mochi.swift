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
        menu.addItem(withTitle: "普通模式", action: #selector(normalMode), keyEquivalent: "")
        menu.addItem(withTitle: "安静模式", action: #selector(quietMode), keyEquivalent: "")
        menu.addItem(.separator())
        menu.addItem(withTitle: "退出 Mochi", action: #selector(quit), keyEquivalent: "q")
        statusItem.menu = menu
    }

    @objc private func normalMode() { pet.quiet = false }
    @objc private func quietMode() { pet.quiet = true }
    @objc private func quit() { NSApp.terminate(nil) }
}

final class MochiPanel: NSPanel {
    var quiet = false { didSet { view.petView.quiet = quiet } }
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
        menu.addItem(withTitle: "普通模式", action: #selector(setNormal), keyEquivalent: "")
        menu.addItem(withTitle: "安静模式", action: #selector(setQuiet), keyEquivalent: "")
        menu.addItem(.separator())
        menu.addItem(withTitle: "退出", action: #selector(quit), keyEquivalent: "")
        NSMenu.popUpContextMenu(menu, with: event, for: view)
    }
    @objc private func setNormal() { quiet = false }
    @objc private func setQuiet() { quiet = true }
    @objc private func quit() { NSApp.terminate(nil) }
}

final class PetHostView: NSView {
    private enum MicroAction { case none, sideLook, sideLie, groom }
    var quiet = false
    private let rest = NSImage(contentsOfFile: Bundle.main.path(forResource: "mochi-rest", ofType: "png", inDirectory: "assets/cats")!)!
    private let blink = NSImage(contentsOfFile: Bundle.main.path(forResource: "mochi-blink", ofType: "png", inDirectory: "assets/cats")!)!
    private let walk = NSImage(contentsOfFile: Bundle.main.path(forResource: "mochi-walk", ofType: "png", inDirectory: "assets/cats")!)!
    private let sideLook = NSImage(contentsOfFile: Bundle.main.path(forResource: "mochi-side-look", ofType: "png", inDirectory: "assets/cats")!)!
    private let sideLie = NSImage(contentsOfFile: Bundle.main.path(forResource: "mochi-side-lie", ofType: "png", inDirectory: "assets/cats")!)!
    private let groom = NSImage(contentsOfFile: Bundle.main.path(forResource: "mochi-groom", ofType: "png", inDirectory: "assets/cats")!)!
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

    override func draw(_ dirtyRect: NSRect) {
        NSColor.clear.setFill(); dirtyRect.fill()
        if walking {
            walkFrame = (walkFrame + 1) % 4
            let frameWidth = walk.size.width / 4
            walk.draw(in: NSRect(x: 17, y: 4, width: 165, height: 131), from: NSRect(x: CGFloat(walkFrame) * frameWidth, y: 120, width: frameWidth, height: 430), operation: .sourceOver, fraction: 1)
        } else if action == .groom {
            let frameWidth = groom.size.width / 3
            groom.draw(in: NSRect(x: 32, y: 28, width: 135, height: 100), from: NSRect(x: CGFloat(groomFrame) * frameWidth, y: 100, width: frameWidth, height: 540), operation: .sourceOver, fraction: 1)
        } else {
            let pose = action == .sideLook ? sideLook : action == .sideLie ? sideLie : Date() < blinkUntil ? blink : rest
            pose.draw(in: NSRect(x: 10, y: 10, width: 180, height: 120), from: NSRect(x: 0, y: 0, width: pose.size.width, height: pose.size.height), operation: .sourceOver, fraction: 1)
        }
    }
}
