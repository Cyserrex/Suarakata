# Suarakata — Speech to Text (Whisper)

**Suarakata** adalah aplikasi desktop **Windows (C# / .NET Framework 4.8, WinForms)**
untuk mengubah rekaman suara (voice note WhatsApp, mp3, dll.) menjadi teks secara
**offline** menggunakan [Whisper.net](https://github.com/sandrohanea/whisper.net)
(binding whisper.cpp dari OpenAI Whisper).

## Fitur

- Transkripsi offline, akurasi tinggi (mendukung Bahasa Indonesia & banyak bahasa lain).
- Pilih model: **Tiny / Base / Small / Medium / Large-v3** (diunduh otomatis sekali).
- Pilih bahasa atau **auto-deteksi**.
- Opsi **sertakan waktu** (timestamp per segmen).
- **Drag & drop** file audio ke jendela aplikasi.
- Tombol **Simpan .txt** dan **Salin**.
- **Mode CLI** untuk otomatisasi / batch.

## Persyaratan

- Windows 10/11 (native runtime Whisper.net direkomendasikan Windows 11+).
- CPU x64 dengan dukungan **AVX, AVX2, FMA, F16C** (CPU modern umumnya mendukung).
- [Microsoft Visual C++ Redistributable (x64)](https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist) versi VS 2022+.
- **ffmpeg** — sudah dibundel di folder aplikasi (`ffmpeg.exe`). Bisa juga dari PATH.
- Untuk build: **.NET SDK 8+** (`dotnet`). Reference assembly net48 disediakan lewat paket NuGet.

## Build

```powershell
dotnet build .\Suarakata.sln -c Release
```

Output di `bin\Release\Suarakata.exe`. Native `whisper.dll` (win-x64) otomatis
tersalin ke `bin\Release\runtimes\`.

> Catatan: letakkan `ffmpeg.exe` di samping `Suarakata.exe` (atau sub-folder
> `ffmpeg\`) agar aplikasi mandiri saat dipindah ke komputer lain.

## Cara pakai (GUI)

1. Jalankan `Suarakata.exe`.
2. Klik **Telusuri...** atau seret file audio ke jendela.
3. Pilih **Model** dan **Bahasa** (default: Small, Indonesia).
4. (Opsional) centang **Sertakan waktu**.
5. Klik **Transkripsi**. Saat pertama kali, model akan diunduh ke folder `models\`.
6. Setelah selesai, klik **Simpan .txt** atau **Salin**.

## Cara pakai (CLI)

```powershell
Suarakata.exe "<file audio>" [model] [bahasa] [output.txt]
```

Contoh:

```powershell
Suarakata.exe "C:\rekaman\pesan.ogg" small id "C:\rekaman\pesan.txt"
```

- `model`: `tiny` | `base` | `small` | `medium` | `large-v3` (default `small`)
- `bahasa`: kode ISO seperti `id`, `en`, `ms`, `auto` (default `id`)
- `output.txt`: opsional, default `<file audio>.stt.txt`

## Model

| Model     | Ukuran unduh | Akurasi | Kecepatan (CPU) |
|-----------|--------------|---------|-----------------|
| Tiny      | ~75 MB       | Rendah  | Sangat cepat    |
| Base      | ~142 MB      | Sedang- | Cepat           |
| Small     | ~466 MB      | Sedang+ | Menengah        |
| Medium    | ~1.5 GB      | Tinggi  | Lambat          |
| Large-v3  | ~3 GB        | Terbaik | Paling lambat   |

Model disimpan di folder `models\` di samping executable. Unduhan berasal dari
Hugging Face; set variabel lingkungan `HF_TOKEN` untuk unduhan lebih cepat/tanpa
rate-limit.

## Bahasa yang tersedia di GUI

Auto-deteksi, Indonesia (`id`), Inggris (`en`), Melayu (`ms`), Jawa (`jw`),
Sunda (`su`), Arab (`ar`), Mandarin (`zh`). Mode CLI menerima kode bahasa apa pun
yang didukung Whisper.

## Struktur proyek

```
Suarakata/
├─ Suarakata.sln
├─ Suarakata.csproj
├─ app.manifest
├─ Program.cs        # Entry point + mode CLI
├─ Transcriber.cs    # Pipeline: unduh model, ffmpeg, Whisper.net
└─ MainForm.cs       # UI WinForms
```

## Troubleshooting

- **"ffmpeg tidak ditemukan"** → letakkan `ffmpeg.exe` di folder aplikasi atau PATH.
- **Gagal load native / crash saat transkripsi** → pasang VC++ Redistributable x64,
  pastikan CPU mendukung AVX2. Untuk CPU tanpa AVX, ganti paket runtime ke
  `Whisper.net.Runtime.NoAvx`.
- **Unduhan model lambat** → set `HF_TOKEN`, atau salin file `ggml-*.bin` secara
  manual ke folder `models\`.
- **Hasil kurang akurat** → gunakan model lebih besar (`medium` / `large-v3`).

## Distribusi (installer)

Untuk mengemas aplikasi ke komputer lain:

1. Jalankan `build-release.ps1` (build Release + salin `ffmpeg.exe` ke `bin\Release`).
2. Buat installer dengan [Inno Setup](https://jrsoftware.org/isdl.php):
   ```powershell
   & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" .\installer\Suarakata.iss
   ```
   Hasil: `installer\Output\Suarakata-Setup-1.0.0.exe`.

Alternatif tanpa installer: cukup **zip seluruh isi `bin\Release`** (termasuk
`Suarakata.exe`, `ffmpeg.exe`, folder `runtimes\`, dan `*.dll`) lalu ekstrak di
komputer tujuan. Model `ggml-*.bin` akan diunduh otomatis saat pertama dipakai.

> Catatan lisensi: `ffmpeg.exe` yang dibundel mengikuti lisensi build-nya
> (LGPL/GPL). Sertakan pemberitahuan lisensi ffmpeg bila mendistribusikan.

## Lisensi

Whisper.net dan whisper.cpp berlisensi MIT. ffmpeg berlisensi LGPL/GPL sesuai build.
