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
    var quiet = false
    private let rest = NSImage(contentsOfFile: Bundle.main.path(forResource: "mochi-rest", ofType: "png", inDirectory: "assets/cats")!)!
    private let walk = NSImage(contentsOfFile: Bundle.main.path(forResource: "mochi-walk", ofType: "png", inDirectory: "assets/cats")!)!
    private var walking = false
    private var blinkUntil = Date.distantPast
    private var nextBlink = Date().addingTimeInterval(5)
    private var nextWalk = Date().addingTimeInterval(45)

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        Timer.scheduledTimer(withTimeInterval: 0.05, repeats: true) { [weak self] _ in self?.tick() }
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }

    private func tick() {
        let now = Date()
        if !quiet && !walking && now >= nextWalk {
            walking = true
            nextWalk = now.addingTimeInterval(TimeInterval(Int.random(in: 40...80)))
            DispatchQueue.main.asyncAfter(deadline: .now() + 1.4) { [weak self] in self?.walking = false }
        }
        if !walking && now >= nextBlink {
            blinkUntil = now.addingTimeInterval(0.22)
            nextBlink = now.addingTimeInterval(TimeInterval(Int.random(in: 7...12)))
        }
        needsDisplay = true
    }

    override func draw(_ dirtyRect: NSRect) {
        NSColor.clear.setFill(); dirtyRect.fill()
        if walking {
            walk.draw(in: NSRect(x: 10, y: 20, width: 180, height: 94), from: NSRect(x: 70, y: 140, width: 1460, height: 760), operation: .sourceOver, fraction: 1)
        } else {
            let sourceX: CGFloat = Date() < blinkUntil ? rest.size.width / 2 : 0
            rest.draw(in: NSRect(x: 10, y: 10, width: 180, height: 114), from: NSRect(x: sourceX, y: 155, width: rest.size.width / 2, height: 560), operation: .sourceOver, fraction: 1)
        }
    }
}
