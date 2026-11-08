import { useEffect, useState } from 'react';

const API_URL = import.meta.env.PUBLIC_API_URL ?? 'https://api.abson.tech';

type Status = 'loading' | 'online' | 'offline';

export default function ApiStatus() {
  const [status, setStatus] = useState<Status>('loading');
  const [message, setMessage] = useState('Conectando à API...');

  useEffect(() => {
    let cancelled = false;

    fetch(`${API_URL}/api/hello`)
      .then((response) => {
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json();
      })
      .then((data) => {
        if (cancelled) return;
        setStatus('online');
        setMessage(data.message ?? 'API online');
      })
      .catch((error: unknown) => {
        if (cancelled) return;
        setStatus('offline');
        setMessage(`API indisponível (${error instanceof Error ? error.message : 'erro'})`);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const label =
    status === 'online' ? 'API online' : status === 'offline' ? 'API offline' : 'Verificando...';

  return (
    <div className={`status ${status}`}>
      <span className="dot" aria-hidden="true" />
      <div>
        <strong>{label}</strong>
        <p>{message}</p>
      </div>
    </div>
  );
}
