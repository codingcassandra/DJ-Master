// Writes the Meta XR Simulator window bounds (global points, top-left origin) to a file, every 0.3 s.
import Foundation
import CoreGraphics
let out = CommandLine.arguments.count > 1 ? CommandLine.arguments[1] : "simwindow.txt"
let once = CommandLine.arguments.contains("--once")
var last = ""
repeat {
    var best: CGRect? = nil
    if let list = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] {
        for w in list {
            guard (w[kCGWindowOwnerName as String] as? String) == "Meta XR Simulator",
                  (w[kCGWindowLayer as String] as? Int) == 0,
                  let b = w[kCGWindowBounds as String] as? [String: CGFloat] else { continue }
            let r = CGRect(x: b["X"]!, y: b["Y"]!, width: b["Width"]!, height: b["Height"]!)
            if best == nil || r.width * r.height > best!.width * best!.height { best = r }
        }
    }
    if let r = best {
        let s = "\(r.minX) \(r.minY) \(r.width) \(r.height)\n"
        if s != last { try? s.write(toFile: out, atomically: true, encoding: .utf8); last = s }
    }
    if !once { usleep(300_000) }
} while !once
