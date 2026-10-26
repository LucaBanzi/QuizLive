import type { MediaType } from '../api/types';

interface Props { type: MediaType; url: string | null; className?: string; controls?: boolean }

/** Immagine, video (autoplay, senza controlli) o audio con icona animata. */
export default function MediaView({ type, url, className = '', controls = false }: Props) {
  if (!url || type === 'None') return null;

  if (type === 'Image')
    return <img key={url} src={url} alt="" className={`h-full w-full object-contain ${className}`} />;

  if (type === 'Video')
    return (
      <video key={url} src={url} autoPlay playsInline controls={controls} className={`h-full w-full object-contain ${className}`} />
    );

  // Audio
  return (
    <div className={`flex h-full w-full items-center justify-center ${className}`}>
      <audio key={url} src={url} autoPlay controls={controls} className={controls ? '' : 'hidden'} />
      <div className="flex h-1/2 items-end gap-[6%]" aria-label="Audio in riproduzione" role="img">
        {[0, 1, 2, 3, 4].map((i) => (
          <span key={i} className="eq-bar block w-[1.2em] rounded-full bg-chalk" style={{ height: '100%', animationDelay: `${i * 140}ms` }} />
        ))}
      </div>
    </div>
  );
}
