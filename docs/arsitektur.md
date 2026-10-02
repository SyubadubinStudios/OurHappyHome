# Arsitektur

```
OurHappyHome.slnx
├─ src/OurHappyHome.Core      simulasi murni (tanpa UI / grafis) — bisa diuji
│  ├─ GameState.cs            semua data yang disimpan
│  ├─ GameSession*.cs         runtime: tick, jadwal, AI, tugas, gerak, interaksi, cerita, pemain
│  ├─ Family/                 anggota, kebutuhan, stamina, mood, keahlian, hubungan, kenangan
│  ├─ World/                  ruangan, rumah (dinding/pintu otomatis), perabot, peta kota, collision, A*
│  ├─ Simulation/             aktivitas, cuaca, hewan, NPC, event bus
│  ├─ Scenarios/              kejadian acak & darurat + EventDirector (mode Santai/Petualangan)
│  ├─ Cooking/ Economy/ School/ Progression/ Time/
│  ├─ SaveSystem.cs, GameSettings.cs, Loc.cs (dua bahasa)
├─ src/OurHappyHome           aplikasi Avalonia + ThreeNet
│  ├─ Rendering/              GameRenderer, HouseView, TownView, CharacterView, Effects, CameraRig,
│  │                          Textures (prosedural), Meshes, ModelLibrary, FurnitureFactory
│  ├─ Audio/                  Synth, SoundBank, MusicComposer, AudioManager
│  ├─ Views/                  MainWindow, MenuScreen, GameScreen (+Hud, Panels, Build), MiniGames,
│  │                          SettingsPanel, AboutScreen, ScreenshotDirector
│  ├─ UI/                     Ui kit, Meter
│  └─ Assets/                 Models (GLB), Voice (MP3), Art (concept art & potret)
├─ tests/OurHappyHome.Tests   xUnit: simulasi, AI, collision, A*, masakan, skenario, simpan/muat
└─ tools/blender              skrip rigging/animasi & optimasi aset (dijalankan via Blender MCP)
```

## Simulasi (`OurHappyHome.Core`)

`GameSession.Tick(realDt)` dipanggil setiap frame:

1. **Waktu** — 1 detik nyata = 1 menit game × kecepatan (1/2/4×, 40× saat semua tidur, 1× saat
   darurat besar). `ProcessSchedule` menjalankan momen tetap per menit: berangkat sekolah/kerja,
   makan bersama (07:00, 18:45), belanja Sabtu, tagihan Senin, gaji Jumat, malam film.
2. **Cuaca** — rantai Markov per beberapa jam, dipengaruhi mode & musim; petir dapat memadamkan listrik.
3. **Kebutuhan** — setiap anggota (dikendalikan atau tidak) kehilangan kenyang, tidur, energi,
   kebersihan, senang-senang dan sosial; kesehatan dan bahagia mengikuti; mood dari *moodlet*.
4. **Anggota** — anggota otonom memilih aktivitas dengan **utility AI** (`GameSession.AI.cs`):
   urgensi kebutuhan × manfaat aktivitas + jadwal + hobi + kenangan + jarak + sedikit acak.
   Tugas = berjalan (A* di `NavGrid` 0,25 m, *string pulling*) → memakai slot perabot (berdiri,
   duduk, berbaring) → hasil (masakan, perbaikan, panen, uang, kenangan…).
5. **Hewan, NPC, skenario, EventDirector, bahaya, tujuan bab & pencapaian.**

Semua yang perlu diketahui presentasi dikirim lewat **`EventBus`**: notifikasi, ucapan (dengan kunci
suara), suara 3D, mood musik, efek partikel, kilat, guncangan, kenangan (minta foto), mini-game, panel.
Sistem saling terhubung lewat state bersama + event, sehingga cerita muncul sendiri (hujan → listrik
padam → Dinda takut → Ayah ke panel listrik → Ibu membuat cokelat hangat → semua berkumpul → kenangan).

**Rumah** dibangun dari slot ruangan tetap; `House.Walls` dan `House.Doors` dihasilkan otomatis
dari kisi 0,25 m (dinding bersama digabung, pintu terbuka hanya jika ruangan di kedua sisi ada).
Collision 2D lingkaran-vs-kotak untuk dinding, perabot, bangunan dan pohon kota.

**Skenario** (`Scenarios/`) punya tujuan, aktor (kucing, monyet, ular, babi hutan, orang asing, polisi,
pemadam, petugas), target interaksi khusus, *hook* (listrik diperbaiki, api padam, diselamatkan…) dan
bisa mengarahkan AI keluarga. Skenario besar menyimpan *snapshot* JSON untuk **Ulangi Kejadian**.

## Rendering (ThreeNet)

`GameRenderer` memiliki `Scene` ThreeNet dan menyamakannya dengan simulasi setiap frame:

- **HouseView** — lantai, dinding dua sisi (cat per ruangan), jendela yang menyala saat malam,
  kusen & pintu depan yang membuka sendiri, atap pelana per ruangan, lampu ruangan, perabot
  (model Rodin yang di-*fit* ke footprint, atau primitif bergaya), bahaya (api + cahaya berkedip,
  asap, genangan, pecahan kaca, puing). **Cutaway**: dinding di sisi kamera turun dan atap hilang saat
  pemain di dalam rumah.
- **TownView** — jalan, rumah tetangga, sekolah, toko dengan papan nama (teks dirender Avalonia),
  taman, pantai, hutan, perkemahan, bianglala & komidi putar berputar, awan, lampu jalan terdekat.
- **CharacterView** — lima GLB ter-rig, 12 klip animasi dengan *cross-fade*, pose duduk/berbaring
  di perabot, label nama, gelembung ucapan, penanda 🆘/💤, cincin pemain, senter (spot light).
- **Effects** — partikel billboard (hati, bintang, konfeti, kembang api, asap, uap, api aditif,
  percikan, tepung, daun, zzz, not musik, koin) dan tirai hujan.
- Pencahayaan: matahari dengan bayangan bertingkat, langit/ambient/kabut mengikuti jam & cuaca,
  kilat, bloom, SSAO, tone mapping ACES. Kualitas grafis dapat diatur.

## Audio

`Synth` membuat semua bunyi secara prosedural; `SoundBank` = ±50 efek + 7 loop ambience (hujan, angin,
burung, jangkrik, api, ombak, keramaian) + suara *babble*; `MusicComposer` menggubah 6 lagu
(santai, jelajah, perayaan, haru, tegang, malam) di thread latar. `AudioManager` memakai mixer spasial
ThreeNet, *cross-fade* musik sesuai situasi, dan memutar rekaman ElevenLabs bila ada.

## UI

Seluruh UI dibangun dalam kode (`UI/Ui.cs`) agar panel bisa dibuat dari data game; skala teks, palet
bantu warna dan kontras tinggi diterapkan di satu tempat. Layar: menu (key art dengan efek Ken Burns),
game (HUD + panel), tentang (kredit bergulir).

## Penyimpanan

`SaveSystem` menulis `GameState` sebagai JSON (enum sebagai string, `Vector2` sebagai array) ke
`%AppData%/OurHappyHome`, termasuk autosave pagi. Foto kenangan disimpan sebagai PNG di folder `photos`.
