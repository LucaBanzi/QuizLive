import { QRCodeSVG } from 'qrcode.react';

/** Il parent decide la dimensione (il QR riempie il contenitore). Sfondo bianco per la leggibilità. */
export default function QrBlock({ url }: { url: string }) {
  return (
    <div className="aspect-square w-full rounded-[6%] bg-white p-[5%]">
      <QRCodeSVG value={url} size={256} level="M" style={{ width: '100%', height: '100%' }} />
    </div>
  );
}
