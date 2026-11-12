# scrcpy — catalogo parametri (v5.0.1)

Fonte: `app/src/cli.c` del repo [Genymobile/scrcpy](https://github.com/Genymobile/scrcpy) al tag `v5.0.1` (ultima release al 2026-10-23).
Tabella opzioni, validazioni (range numerici) e incompatibilità sono estratte direttamente dal parser.

Legenda colonna **UI**:

- **Toggle** — flag senza argomento (`--opzione` presente/assente)
- **Combo** — valori chiusi → ComboBox
- **Number** — NumberBox (range tra parentesi)
- **Text** — TextBox (formato indicato)
- **Bitrate** — Text/Number con suffissi `K` (×1000) e `M` (×1000000)
- **Opt-arg** — opzione con argomento *facoltativo* (`--opzione` oppure `--opzione=valore`) → Toggle + TextBox

> Gli argomenti con valore vanno passati come `--opzione=valore` (obbligatorio per gli opt-arg, consigliato per tutti).

---

## 1. Connessione / selezione dispositivo

| Opzione | UI | Valori / default | Descrizione |
|---|---|---|---|
| `-s, --serial` | Text | seriale adb | Seriale del dispositivo. Obbligatorio solo se più dispositivi sono collegati ad adb. |
| `-d, --select-usb` | Toggle | — | Usa il dispositivo USB (se ce n'è esattamente uno, come `adb -d`). |
| `-e, --select-tcpip` | Toggle | — | Usa il dispositivo TCP/IP (se ce n'è esattamente uno, come `adb -e`). |
| `--tcpip` | Opt-arg | `[+]ip[:porta]`, porta default 5555 | Configura e connette il device via TCP/IP. Con indirizzo: si connette a quello. Senza: trova l'IP del device (di solito collegato via USB), abilita TCP/IP e si connette. Prefisso `+` forza la riconnessione. |
| `--kill-adb-on-close` | Toggle | — | Termina adb alla chiusura di scrcpy. |

⚠ Al massimo **un** selettore tra `-s`, `-d`, `-e`, `--tcpip=<addr>` → in UI conviene un RadioButtons "Modalità connessione".

## 2. Video

| Opzione | UI | Valori / default | Descrizione |
|---|---|---|---|
| `--video-source` | Combo | `display` \| `camera` — default `display` | Sorgente video. La camera richiede Android 12+. |
| `--video-codec` | Combo | `h264` \| `h265` \| `av1` \| `vp8` \| `vp9` — default `h264` | Codec video. |
| `-b, --video-bit-rate` | Bitrate | 0–2147483647 — default `8M` | Bitrate di encoding video (bit/s). |
| `-m, --max-size` | Number (0–65535) | default `0` (illimitato) | Limita larghezza e altezza del video; l'altra dimensione è calcolata mantenendo l'aspect ratio. In modalità camera seleziona la dimensione della sorgente. |
| `--max-fps` | Number | — | Limita il frame rate di cattura (supportato ufficialmente da Android 10). |
| `--video-buffer` | Number ms (0–3600000) | default `0` | Ritardo di buffering prima di mostrare i frame: più latenza, meno jitter. |
| `--video-encoder` | Text / Combo* | nome encoder MediaCodec | Encoder specifico (dipende da `--video-codec`). Elenco con `--list-encoders`. |
| `--video-codec-options` | Text | `key[:type]=value[,...]`, type: `int`(default) \| `long` \| `float` \| `string` | Opzioni per l'encoder video del device (chiavi di [MediaFormat](https://d.android.com/reference/android/media/MediaFormat)). |
| `--crop` | Text | `width:height:x:y` | Ritaglia lo schermo lato server. Valori nell'orientamento naturale del device. |
| `--angle` | Number | gradi | Ruota il contenuto video di un angolo custom (orario). |
| `--capture-orientation` | Combo + check "@" | `0` `90` `180` `270` `flip0` `flip90` `flip180` `flip270`, opz. prefisso `@` (o `@` da solo) — default `0` | Orientamento di cattura. `flip` = flip orizzontale prima della rotazione. `@90` blocca la rotazione relativa all'orientamento naturale; `@` da solo la blocca su quello iniziale. |
| `--display-orientation` | Combo | `0` `90` `180` `270` `flip0` `flip90` `flip180` `flip270` — default `0` | Orientamento iniziale della visualizzazione. |
| `--orientation` | Combo | come sopra | Scorciatoia per `--display-orientation` + `--record-orientation`. |
| `--min-size-alignment` | Combo | `1` \| `2` \| `4` \| `8` \| `16` — default `1` | Allineamento minimo (potenza di 2) di larghezza/altezza del video. |
| `--ignore-video-encoder-constraints` | Toggle | — | Ignora le capacità dichiarate dall'encoder (utile se sono errate). |
| `--no-downsize-on-error` | Toggle | — | Su errore MediaCodec non riprovare automaticamente con risoluzione più bassa. |
| `--no-video` | Toggle | — | Disabilita l'inoltro video. |
| `--no-video-playback` | Toggle | — | Disabilita la riproduzione video sul PC (il video viene comunque catturato, es. per registrare). |
| `--hwdec` | Combo | `auto` \| `disabled` \| `d3d11va` — default `auto` | Decodifica hardware sul PC. (`vaapi` solo Linux, `videotoolbox` solo macOS → da escludere su Windows.) |

\* Combo popolabile eseguendo `scrcpy --list-encoders`.

## 3. Audio

| Opzione | UI | Valori / default | Descrizione |
|---|---|---|---|
| `--audio-source` | Combo | `output` \| `playback` \| `mic` \| `mic-unprocessed` \| `mic-camcorder` \| `mic-voice-recognition` \| `mic-voice-communication` \| `voice-call` \| `voice-call-uplink` \| `voice-call-downlink` \| `voice-performance` — default `output` | Sorgente audio (vedi tabella sotto). |
| `--audio-codec` | Combo | `opus` \| `aac` \| `flac` \| `raw` — default `opus` | Codec audio. |
| `--audio-bit-rate` | Bitrate | default `128K` | Bitrate audio (ignorato per `flac` e `raw`). |
| `--audio-buffer` | Number ms (0–3600000) | default `50` | Ritardo di buffering audio. Più basso = meno latenza ma più rischio di glitch. |
| `--audio-output-buffer` | Number ms (0–1000) | default `10` | Dimensione del buffer di output SDL. Da non toccare senza motivo. |
| `--audio-encoder` | Text / Combo* | nome encoder | Encoder MediaCodec specifico (dipende da `--audio-codec`). |
| `--audio-codec-options` | Text | `key[:type]=value[,...]` | Opzioni per l'encoder audio del device. |
| `--audio-dup` | Toggle | — | Duplica l'audio: cattura e continua a riprodurlo anche sul device. Solo con `--audio-source=playback`. |
| `--no-audio` | Toggle | — | Disabilita l'inoltro audio. |
| `--no-audio-playback` | Toggle | — | Disabilita la riproduzione audio sul PC. |
| `--require-audio` | Toggle | — | Fallisce se l'audio è abilitato ma non funziona (di default prosegue solo con il video). |

Valori di `--audio-source`:

| Valore | Significato |
|---|---|
| `output` | Inoltra tutto l'output audio e disattiva la riproduzione sul device. |
| `playback` | Cattura la riproduzione audio (le app possono escludersi, quindi non è garantito sia tutto). |
| `mic` | Microfono. |
| `mic-unprocessed` | Microfono non processato (raw). |
| `mic-camcorder` | Microfono ottimizzato per registrazione video. |
| `mic-voice-recognition` | Microfono ottimizzato per riconoscimento vocale. |
| `mic-voice-communication` | Microfono per comunicazioni (cancellazione eco, AGC se disponibili). |
| `voice-call` | Chiamata vocale. |
| `voice-call-uplink` | Solo uplink della chiamata. |
| `voice-call-downlink` | Solo downlink della chiamata. |
| `voice-performance` | Audio per performance live (karaoke): microfono + riproduzione. |

## 4. Camera (solo con `--video-source=camera`)

| Opzione | UI | Valori / default | Descrizione |
|---|---|---|---|
| `--camera-id` | Text / Combo* | id | Id della camera (elenco con `--list-cameras`). |
| `--camera-facing` | Combo | `front` \| `back` \| `external` | Seleziona la camera per direzione. Alternativo a `--camera-id`. |
| `--camera-size` | Text | `<width>x<height>` | Dimensione di cattura esplicita (elenco con `--list-camera-sizes`). Incompatibile con `-m` e `--camera-ar`. |
| `--camera-ar` | Text / Combo editabile | `sensor` \| `<num>:<den>` (es. `4:3`) \| `<valore>` (es. `1.6`) | Seleziona la dimensione per aspect ratio (±10%). |
| `--camera-fps` | Number (0–65535) | default Android (30) | Frame rate di cattura della camera. |
| `--camera-high-speed` | Toggle | — | Modalità high-speed (risoluzioni/fps limitati). Richiede `--camera-fps` esplicito. |
| `--camera-zoom` | Number (decimale) | — | Zoom iniziale. |
| `--camera-torch` | Toggle | — | Accende il flash all'avvio della camera. |

## 5. Display (sorgente / display virtuale)

| Opzione | UI | Valori / default | Descrizione |
|---|---|---|---|
| `--display-id` | Number / Combo* | default `0` | Display del device da mostrare (elenco con `--list-displays`). Solo con `--video-source=display`. |
| `--new-display` | Opt-arg | `[<width>x<height>][/<dpi>]` es. `1920x1080`, `1920x1080/420`, `/240`, vuoto = come display principale | Crea un nuovo display virtuale. Incompatibile con `--display-id` e `--no-video`. |
| `-x, --flex-display` | Toggle | — | Ridimensiona continuamente il display virtuale per adattarlo alla finestra. Solo con `--new-display`; incompatibile con `--no-control`, `--crop`, `--window-width/height`. |
| `--display-ime-policy` | Combo | `local` \| `fallback` \| `hide` | Dove mostrare la tastiera virtuale (IME): sul display locale, sul display di fallback (principale) o nasconderla. Solo su display secondario. |
| `--no-vd-destroy-content` | Toggle | — | Alla chiusura del display virtuale le app vengono spostate sul display principale invece che distrutte. |
| `--no-vd-system-decorations` | Toggle | — | Disabilita le decorazioni di sistema sul display virtuale. |
| `--start-app` | Text / Combo* | package name; prefisso `?` = ricerca per nome (case-insensitive), `+` = force-stop prima, `+?` entrambi | Avvia un'app Android (elenco con `--list-apps`). Richiede il controllo abilitato. |

## 6. Input e controllo

| Opzione | UI | Valori / default | Descrizione |
|---|---|---|---|
| `-n, --no-control` | Toggle | — | Mirroring in sola lettura (nessun controllo del device). |
| `--keyboard` | Combo | `disabled` \| `sdk` \| `uhid` \| `aoa` | Come inviare la tastiera: `sdk` = API Android; `uhid` = tastiera HID fisica simulata (UHID kernel); `aoa` = protocollo AOAv2 (solo USB). Con `uhid`/`aoa` il layout va configurato sul device (Impostazioni → Sistema → Lingue → Tastiera fisica). |
| `--mouse` | Combo | `disabled` \| `sdk` \| `uhid` \| `aoa` | Come inviare il mouse. In `uhid`/`aoa` il mouse viene catturato (modalità relativa); LAlt/LSuper/RSuper rilasciano la cattura. `sdk` richiede la riproduzione video. |
| `--gamepad` | Combo | `disabled` \| `uhid` \| `aoa` | Come inviare i gamepad (HID simulato via UHID o AOAv2). |
| `-K` / `-M` / `-G` | — | — | Scorciatoie per `--keyboard`/`--mouse`/`--gamepad` = `uhid` (o `aoa` se `--otg`). Nella UI basta la Combo. |
| `--mouse-bind` | Text (o 4+4 Combo) | `xxxx[:xxxx]`, ogni carattere: `+` inoltra, `-` ignora, `b` BACK, `h` HOME, `s` APP_SWITCH, `n` notifiche — default `bhsn:++++` (sdk), `++++:bhsn` (uhid/aoa) | Binding dei click secondari (destro, centrale, 4°, 5°). Seconda sequenza = con Shift premuto. |
| `--no-mouse-hover` | Toggle | — | Non inoltrare il movimento del mouse senza click. Solo `--mouse=sdk`. |
| `--prefer-text` | Toggle | — | Inietta lettere e spazio come eventi testo (aiuta con caratteri speciali, rompe WASD nei giochi). Solo `--keyboard=sdk`; incompatibile con `--raw-key-events`. |
| `--raw-key-events` | Toggle | — | Inietta eventi tasto per tutti i tasti, ignora eventi testo. Solo `--keyboard=sdk`. |
| `--no-key-repeat` | Toggle | — | Non inoltrare la ripetizione tasti tenuti premuti. Solo `--keyboard=sdk`. |
| `--legacy-paste` | Toggle | — | Ctrl+V incolla iniettando il testo come sequenza di tasti (workaround per device problematici). |
| `--no-clipboard-autosync` | Toggle | — | Disabilita la sincronizzazione automatica degli appunti PC ↔ device. |
| `--shortcut-mod` | Multi-check | `lctrl` `rctrl` `lalt` `ralt` `lsuper` `rsuper`, separati da `,` — default `lalt,lsuper` | Modificatori per le scorciatoie di scrcpy (combinazioni con `+` non più supportate). |
| `--otg` | Toggle | — | Modalità OTG: simula tastiera/mouse fisici, niente adb né mirroring. Solo USB. In OTG le input accettano solo `aoa`/`disabled`. |

⚠ **Windows**: `--keyboard=aoa` e `--mouse=aoa` funzionano solo in modalità `--otg` (adb tiene già aperto il device USB).

## 7. Finestra (lato PC)

| Opzione | UI | Valori / default | Descrizione |
|---|---|---|---|
| `--window-title` | Text | default = modello device | Titolo della finestra. **Utile all'app** per riconoscere la finestra del profilo. |
| `--window-x` | Number (−32767–32767) o `auto` | default `auto` | Posizione orizzontale iniziale. |
| `--window-y` | Number (−32767–32767) o `auto` | default `auto` | Posizione verticale iniziale. |
| `--window-width` | Number (0–65535) | default `0` (auto) | Larghezza iniziale. |
| `--window-height` | Number (0–65535) | default `0` (auto) | Altezza iniziale. |
| `--window-borderless` | Toggle | — | Finestra senza bordi/decorazioni. |
| `--always-on-top` | Toggle | — | Finestra sempre in primo piano. |
| `-f, --fullscreen` | Toggle | — | Avvia a schermo intero. |
| `--no-window-aspect-ratio-lock` | Toggle | — | Non bloccare l'aspect ratio della finestra. |
| `--render-fit` | Combo | `letterbox` \| `stretched` \| `unscaled` — default `letterbox` (`unscaled` con `--flex-display`) | Come il rendering si adatta alla finestra: bande nere / stirato / senza scala. |
| `--background-color` | ColorPicker | `#RGB` o `#RRGGBB` — default `#222` | Colore di sfondo. |
| `--render-driver` | Combo | `direct3d` \| `opengl` \| `opengles2` \| `opengles` \| `software` (`metal` solo macOS) | Hint a SDL sul driver di rendering. |
| `--no-mipmaps` | Toggle | — | Disabilita i mipmap (migliorano la qualità del downscaling con OpenGL). |
| `--no-window` | Toggle | — | Nessuna finestra (implica `--no-video-playback`). Utile solo per registrare/audio. |
| `-N, --no-playback` | Toggle | — | Nessuna riproduzione audio/video sul PC (= `--no-video-playback --no-audio-playback`). |
| `--disable-screensaver` | Toggle | — | Disabilita lo screensaver del PC mentre scrcpy è attivo. |

## 8. Comportamento del dispositivo

Tutte richiedono il controllo abilitato (no `--no-control`).

| Opzione | UI | Valori / default | Descrizione |
|---|---|---|---|
| `-S, --turn-screen-off` | Toggle | — | Spegne subito lo schermo del device (il mirroring continua). |
| `-w, --stay-awake` | Toggle | — | Tiene acceso il device mentre è collegato e scrcpy è attivo. |
| `--keep-active` | Toggle | — | Tiene lo schermo acceso simulando attività utente. |
| `--screen-off-timeout` | Number secondi (0–2147483) | — | Timeout spegnimento schermo durante la sessione (ripristinato all'uscita). |
| `-t, --show-touches` | Toggle | — | Abilita "mostra tocchi" (solo tocchi fisici), ripristinato all'uscita. |
| `--power-off-on-close` | Toggle | — | Spegne lo schermo del device alla chiusura di scrcpy. |
| `--no-power-on` | Toggle | — | Non accendere il device all'avvio. |
| `--no-cleanup` | Toggle | — | Non rimuovere il server dal device e non ripristinare lo stato (show touches, stay awake, power mode) all'uscita. |
| `--push-target` | Text | default `/sdcard/Download/` | Cartella di destinazione per il drag & drop di file sul device. |

## 9. Registrazione

| Opzione | UI | Valori / default | Descrizione |
|---|---|---|---|
| `-r, --record` | Text + FileSavePicker | percorso file (es. `file.mp4`) | Registra su file. Formato da `--record-format` o dall'estensione. |
| `--record-format` | Combo | `mp4` \| `mkv` \| `m4a` \| `mka` \| `opus` \| `aac` \| `flac` \| `wav` | Forza il formato. Richiede `--record`. |
| `--record-orientation` | Combo | `0` \| `90` \| `180` \| `270` — default `0` | Rotazione della registrazione (niente flip). |
| `--time-limit` | Number secondi | — | Durata massima del mirroring. |

Vincoli: `opus`/`aac`/`flac`/`wav` richiedono rispettivamente audio codec `opus`/`aac`/`flac`/`raw` e solo audio; `mp4` non supporta audio `raw` né video `vp8`; `m4a`/`mka`/... sono solo audio.

## 10. Rete / tunnel adb (avanzate)

| Opzione | UI | Valori / default | Descrizione |
|---|---|---|---|
| `-p, --port` | Text | `porta[:porta]` (0–65535) — default `27183:27199` | Porta (o range) TCP su cui ascolta il client. |
| `--force-adb-forward` | Toggle | — | Non tentare `adb reverse`, usa `adb forward`. |
| `--tunnel-host` | Text | IP — default `localhost` | IP del tunnel adb per raggiungere il server (abilita `--force-adb-forward`). |
| `--tunnel-port` | Number (0–65535) | default `0` (non forzata) | Porta del tunnel adb (abilita `--force-adb-forward`). |

## 11. Log / diagnostica

| Opzione | UI | Valori / default | Descrizione |
|---|---|---|---|
| `-V, --verbosity` | Combo | `verbose` \| `debug` \| `info` \| `warn` \| `error` — default `info` | Livello di log. |
| `--print-fps` | Toggle | — | Stampa il framerate in console (MOD+i lo attiva/disattiva). Richiede riproduzione video. |
| `--pause-on-exit` | Opt-arg/Combo | `true` \| `false` \| `if-error` — default `false` | Pausa la console all'uscita per leggere gli errori. Con una GUI che cattura stdout/stderr è superfluo. |
| `--no-terminal-title` | Toggle | — | Non aggiornare il titolo della console. Irrilevante da GUI. |

---

## Comandi di utilità (non fanno parte del profilo)

Eseguono, stampano su stdout ed escono. L'app li può usare per **popolare le ComboBox** (marcate con `*` sopra).

| Comando | Uso nell'app |
|---|---|
| `--list-encoders` | Combo `--video-encoder` / `--audio-encoder` |
| `--list-displays` | Combo `--display-id` |
| `--list-cameras` | Combo `--camera-id` |
| `--list-camera-sizes` | Combo `--camera-size` |
| `--list-apps` | Combo `--start-app` |
| `-v, --version` | Verifica versione di scrcpy installata |
| `-h, --help` | — |

Elenco device collegati: non è un'opzione di scrcpy → `adb devices -l`.

## Escluse su Windows

`--v4l2-sink`, `--v4l2-buffer` (solo Linux), `--hwdec=vaapi`, `--hwdec=videotoolbox`, `--render-driver=metal`.

## Variabili d'ambiente

| Variabile | Descrizione |
|---|---|
| `ADB` | Percorso dell'eseguibile adb |
| `ANDROID_SERIAL` | Seriale da usare se non c'è un selettore (`-s`, `-d`, `-e`, `--tcpip=<addr>`) |
| `SCRCPY_ICON_DIR` | Cartella delle icone |
| `SCRCPY_SERVER_PATH` | Percorso del binario server |

## Exit code

| Codice | Significato |
|---|---|
| `0` | Terminazione normale |
| `1` | Errore di avvio (opzioni invalide, device non trovato, …) |
| `2` | Device disconnesso durante l'esecuzione |

## Dipendenze e incompatibilità (da validare nella UI)

- Un solo selettore device tra `-s`, `-d`, `-e`, `--tcpip=<addr>`.
- `--no-video` + `--no-audio` + `--no-control` senza `--otg` → "nothing to do".
- `--hwdec` richiede la riproduzione video; `--print-fps` idem.
- `--mouse=sdk` richiede la riproduzione video.
- `--prefer-text`, `--raw-key-events`, `--no-key-repeat` → solo `--keyboard=sdk`; `--prefer-text` ⟂ `--raw-key-events`.
- `--no-mouse-hover` → solo `--mouse=sdk`.
- Opzioni `--camera-*` → solo `--video-source=camera`; `--camera-id` ⟂ `--camera-facing`; `--camera-size` ⟂ `-m` e ⟂ `--camera-ar`; `--camera-high-speed` richiede `--camera-fps`.
- `--display-id`, `--new-display`, `--display-ime-policy` → solo `--video-source=display`; `--display-id` ⟂ `--new-display`; `--new-display` ⟂ `--no-video`.
- `--display-ime-policy` → solo su display secondario (`--display-id` ≠ 0 o `--new-display`).
- `-x/--flex-display` → solo con `--new-display`; ⟂ `--no-control`, `--crop`, `--window-width/height`.
- `--audio-dup` → solo `--audio-source=playback` e audio abilitato.
- `--record-format` / `--record-orientation` richiedono `--record`; `--record-orientation` non accetta flip.
- Con `--no-control` non sono ammessi: `-S`, `-w`, `-t`, `--power-off-on-close`, `--start-app`, `--keep-active`.
- `--otg`: niente `--no-control`, `--record`, `-S`, `-w`, `-t`, `--power-off-on-close`, `--display-id`; input solo `aoa`/`disabled` e non tutti disabilitati.
- Windows: `--keyboard=aoa` / `--mouse=aoa` solo con `--otg`.
