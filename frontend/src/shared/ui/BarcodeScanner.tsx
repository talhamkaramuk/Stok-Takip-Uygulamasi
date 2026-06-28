import type { IScannerControls } from "@zxing/browser";
import { Camera } from "lucide-react";
import { useEffect, useRef, useState } from "react";

type BarcodeReader = InstanceType<typeof import("@zxing/browser").BrowserMultiFormatReader>;

export function BarcodeScanner({ onDetect }: { onDetect: (value: string) => void }) {
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const controlsRef = useRef<IScannerControls | null>(null);
  const readerRef = useRef<BarcodeReader | null>(null);
  const [active, setActive] = useState(false);
  const [cameraMessage, setCameraMessage] = useState<string | null>(null);

  async function start() {
    setCameraMessage(null);

    if (!videoRef.current) {
      setCameraMessage("Kamera alanı hazırlanamadı. Sayfayı yenileyip tekrar deneyin.");
      return;
    }

    try {
      const { BrowserMultiFormatReader } = await import("@zxing/browser");
      const reader = readerRef.current ?? new BrowserMultiFormatReader();
      readerRef.current = reader;
      const controls = await reader.decodeFromVideoDevice(undefined, videoRef.current, (result) => {
        if (!result) {
          return;
        }

        onDetect(result.getText());
        stop();
      });
      controlsRef.current = controls;
      setActive(true);
    } catch {
      stop();
      setCameraMessage("Kamera açılamadı. Tarayıcı kamera iznini kontrol edin veya manuel barkod alanını kullanın.");
    }
  }

  function stop() {
    controlsRef.current?.stop();
    controlsRef.current = null;
    setActive(false);
  }

  useEffect(() => {
    return () => stop();
  }, []);

  return (
    <div className="scanner-box">
      <video ref={videoRef} muted playsInline />
      <button className={active ? "ghost-action" : "primary-action"} type="button" onClick={() => (active ? stop() : void start())}>
        <Camera size={17} />
        {active ? "Kamerayı kapat" : "Kamera"}
      </button>
      {cameraMessage && <span className="inline-warning">{cameraMessage}</span>}
    </div>
  );
}
