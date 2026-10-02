# Aset & pipeline

Semua aset dibuat khusus untuk game ini.

```
art/
├─ concept/   concept art (Rodin MCP: Nano Banana 2 / Qwen Image)
├─ raw/       model 3D mentah dari Rodin (Generate3DFromPrompt), T-pose untuk karakter
├─ rigged/    5 karakter ter-rig + 12 animasi (Blender MCP)
├─ props/     perabot & hewan yang sudah dioptimasi (Blender)
└─ voice/     suara karakter (ElevenLabs v3 via Rodin MCP)
tools/blender/
├─ rig_character.py   rigging + skinning + animasi + ekspor GLB
├─ optimize_prop.py   decimate + kompres tekstur + ekspor GLB
├─ preview.py / preview_anim.py   render pratinjau pose
```

## 1. Concept art (Rodin MCP)

| | |
|---|---|
| ![](images/concept-key-art.jpg) | ![](images/concept-family-sheet.jpg) |
| Key art (layar menu) | Lembar karakter (sumber potret HUD) |
| ![](images/concept-home-interior.jpg) | ![](images/concept-world-map.jpg) |
| Interior rumah (layar loading) | Peta kota (panel peta) |

Potret bulat di HUD dipotong otomatis dari lembar karakter, dan ikon aplikasi dibuat dari key art.

## 2. Model 3D (Rodin MCP)

Karakter dibuat dengan `Generate3DFromPrompt` dalam **T-pose** (lengan lurus ke samping) supaya mudah
di-rig: Ayah, Ibu, Kak Nara, Raka dan Dinda, sesuai deskripsi pakaian di dokumen desain. Selain itu
ada 14 aset: sofa, ranjang loteng, ranjang ganda, konter dapur, kulkas, set meja makan, meja belajar,
mobil, ayunan, lemari TV, pohon bunga, rak buku, anjing dan kucing.

![](images/rodin-props.png)

Semua prop menghadap +Z. `optimize_prop.py` menurunkannya ke ±6.000 face dan tekstur 1024 px JPEG,
sehingga ukurannya turun dari ±9 MB ke ±0,4 MB per model. Di game, `ModelLibrary` mengimpor setiap GLB
sekali lalu membuat salinan dengan `Node.Clone`, diskalakan agar pas dengan footprint perabot.

## 3. Rigging & animasi (Blender MCP)

`tools/blender/rig_character.py` dijalankan lewat `execute_blender_code` Blender MCP di Blender 5.2:

```python
exec(open(r".../tools/blender/rig_character.py").read(), ns)
ns["process"](r"art/raw/father.glb", r"art/rigged/father.glb", 1.74)
```

Skrip ini:

1. Menggabungkan dan membersihkan mesh, lalu men-decimate ke 14.000 face dan memperkecil tekstur.
2. Menaruh model di lantai dengan tinggi sesuai usia (1,74 / 1,64 / 1,42 / 1,36 / 1,22 m).
3. Mencari sendi dari bentuk mesh: garis lengan, leher tersempit, selangkangan (dengan cadangan
   proporsi untuk rok) dan posisi kaki.
4. Membuat armature humanoid dengan 19 tulang dan *skinning* bone-heat, plus bobot berbasis jarak
   untuk verteks yang terlewat (maksimal 4 pengaruh).
5. Membuat **12 aksi**: Idle, Walk, Run, Wave, Sit, Sleep, Cook, Cheer, Scared, Talk, Work, Read.
   Rotasinya ditulis dalam ruang armature sehingga tidak bergantung pada *roll* tulang.
6. Mengekspor GLB (skin + semua aksi) yang dibaca ThreeNet sebagai klip animasi.

![](images/rig-father-poses.png)
![](images/rig-family-poses.png)

Di game, `CharacterView` memutar klip dengan *cross-fade* bobot, menyesuaikan tinggi pinggul saat
duduk, dan memutar badan saat berbaring di ranjang.

## 4. Suara karakter

Kalimat penting direkam dengan `GenerateVoiceWithElevenLabsV3` (Bahasa Indonesia). Saat ini tersedia
`mom_breakfast`, `dad_greet`, `dad_fix`, `dad_storm`, `dad_safe` dan `dad_cheer` di
`src/OurHappyHome/Assets/Voice/`. Kunci lain yang dipakai kode, misalnya `ys_scared`, `os_cat`,
`mom_dinner` dan `boy_found`, akan otomatis memakai file MP3 bernama sama jika ditambahkan. Kalimat
tanpa rekaman memakai **suara babble sintetis** dengan nada khas tiap karakter, dan suara anak
dinaikkan nadanya sedikit. Subtitle selalu tersedia.

## 5. Musik & efek suara (prosedural)

Tidak ada file musik. `Audio/MusicComposer.cs` menggubah 6 loop saat game mulai:

| Mood | Tempo / kunci | Instrumen |
|---|---|---|
| Santai | 92 BPM, C mayor | piano arpeggio, bass, shaker |
| Jelajah | 112 BPM, G mayor | ukulele (Karplus-Strong), lonceng FM, drum pop |
| Perayaan | 124 BPM, F mayor | lead, stab, tepuk tangan |
| Haru | 72 BPM, A minor | pad, piano |
| Tegang | 104 BPM, D minor | ostinato petik, pad gelap, tik-tik |
| Malam | 76 BPM, Eb mayor | kotak musik, pad |

`Audio/SoundBank.cs` membuat sekitar 50 efek suara (klik, langkah, petir, gonggongan, meong, sirene,
alarm, alat pemadam, kaca pecah, koin, fanfare, dan lainnya) serta 7 loop ambience.
