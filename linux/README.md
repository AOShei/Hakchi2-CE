# hakchi2 CE on Linux

hakchi2 CE is a .NET Framework 4.8 Windows Forms application. On Linux it runs
under the [Mono](https://www.mono-project.com/) runtime. Day-to-day use (adding
games, syncing over the console's USB network link or Wi-Fi, installing mods)
works the same as on Windows. Differences are listed at the bottom.

## Running a release

1. Install Mono and libusb from your distribution:
   - Arch / EndeavourOS / Manjaro: `sudo pacman -S mono libusb`
   - Debian / Ubuntu: `sudo apt install mono-complete libusb-1.0-0`
   - Fedora: `sudo dnf install mono-complete libusb1`
2. Extract the Linux release archive anywhere and run `./hakchi.sh`.
3. Install the udev rule and the NetworkManager profile (both come with the
   release):
   ```sh
   sudo ./install-system.sh
   ```
   The udev rule lets hakchi open the console over USB in FEL mode and names
   the console's USB network interface `hakchi0`. The NetworkManager profile
   gives that interface a link-local (169.254.x.x) address: the console sits
   at 169.254.13.37 and runs no DHCP server, so without the profile the host
   waits for a DHCP lease forever and hakchi never sees the console. Windows
   does this automatically; Linux needs to be told.

## First-time install on a console

A console on stock firmware shows up in `lsusb` as `1f3a:efe8` and in the
kernel log as "Nintendo CLV-S-SNPY" even when booted normally. That device is
inert: hakchi cannot talk to it, and the status bar stays "Offline". hakchi
has to be installed once through FEL mode:

1. Start hakchi and choose **Kernel → Install / Repair**.
2. When asked for FEL mode: with the USB cable connected, power the console
   off, hold **Reset**, power it on, keep Reset held for about five seconds,
   then release. The power LED stays off in FEL mode.
3. hakchi dumps the original kernel first (keep it), then flashes its own.

After that the console boots with a USB network interface. Linux configures it
automatically and hakchi finds the console through mDNS, with avahi running.

## Troubleshooting

**The console shows the hakchi screen but hakchi keeps "waiting for device".**
While it sits at that screen the console is on USB as `04e8:6863`
"hakchi classic", a USB network gadget. Check, in this order:

1. The interface exists and has a 169.254.x.x address:
   ```sh
   ip -br addr show hakchi0
   ```
   No `hakchi0` at all but an `enp…u…` interface instead: the udev rule is not
   installed, run `sudo ./install-system.sh` and replug the console. An
   interface without an IPv4 address: the NetworkManager profile is missing or
   not loaded (`nmcli connection show "hakchi USB"`).
2. The console answers: `ping -c1 169.254.13.37`. hakchi also tries this
   address directly (log line "USB link: console answering"), so if the ping
   works the console should connect even when mDNS does not.
3. The console announces itself over mDNS: `avahi-browse -rt _ssh._tcp`
   should list `hakchi`. If the ping works but this does not, a firewall is
   dropping multicast DNS. With firewalld the shipped profile puts the link in
   the `trusted` zone; check with `firewall-cmd --get-active-zones`.
4. The host kernel can load the USB network drivers:
   ```sh
   modinfo rndis_host cdc_ether usbnet
   ```
   If they are reported missing, the usual cause on Arch-based distributions
   is a kernel update since the last reboot: the running kernel's modules were
   removed with the old package. Reboot and try again.

**Discovery over Wi-Fi.** The console announces `_ssh._tcp` over mDNS on the
LAN as well. Avahi must be running (`systemctl status avahi-daemon`), and a
firewall on the LAN interface has to let multicast DNS in (for firewalld:
`sudo firewall-cmd --permanent --add-service=mdns && sudo firewall-cmd --reload`).

## Building from source

Requirements: `mono`, `mono-msbuild` (Debian/Ubuntu: `mono-complete msbuild`
from the Mono repository), `git`, `wget`, `tar`.

```sh
git clone --recurse-submodules https://github.com/AOShei/Hakchi2-CE.git
cd Hakchi2-CE
./linux/build.sh            # Release build
./linux/build.sh Debug      # or a Debug build
```

The build downloads the ROM hash database, the hakchi hmod and the fallback
kernel on first run. The result lands in `hakchi_gui/bin/Release/net48/` and
can be started with the `hakchi.sh` placed there.

## What differs from the Windows build

- The read-me and message-of-the-day panels show plain text instead of
  rendered HTML (Mono has no embedded web browser control).
- The "Open FTP" and "Open Telnet" menu items hand `ftp://` and `telnet://`
  URLs to your desktop (`xdg-open`). Most file managers open FTP directly; for
  telnet you may need to register a terminal handler or use `ssh root@<ip>`
  (password `clover`) instead.
- The Windows auto-updater is disabled; pick up new versions from the releases
  page.
- The optional SNES "SFROM Tool" is a Windows executable and is not used.
- The Windows USB driver installer does not exist here; the udev rule and
  NetworkManager profile installed by `install-system.sh` replace it.
