import { Injectable, computed, effect, signal } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { Availability, DataManifest, DetailsPayload } from './model';

const SECRET_STORAGE_KEY = 'detailsSecret';
const RELOAD_INTERVAL_MS = 5 * 60 * 1000;

@Injectable({ providedIn: 'root' })
export class AvailabilityStore {
  private readonly reloadToken = signal(Date.now());
  private readonly detailsHash = signal<string | null>(null);
  private candidateSecret: string | null = null;

  readonly manifest = httpResource<DataManifest>(() => ({
    url: 'data/data-manifest.json',
    params: { t: this.reloadToken() },
  }));

  readonly availability = httpResource<Availability>(() => {
    const manifest = this.manifest.value();
    return manifest ? `data/${manifest.availability}` : undefined;
  });

  readonly details = httpResource<DetailsPayload>(() => {
    const manifest = this.manifest.value();
    const hash = this.detailsHash();
    return manifest && hash ? `data/${manifest.detailsPrefix}-${hash}.json` : undefined;
  });

  readonly detailsUnlocked = computed(() => this.details.hasValue());
  readonly secretRejected = signal(false);

  constructor() {
    effect(() => {
      if (this.details.hasValue() && this.candidateSecret) {
        store(this.candidateSecret);
        this.candidateSecret = null;
        this.secretRejected.set(false);
      }
    });

    effect(() => {
      if (this.details.error()) {
        forget();
        this.detailsHash.set(null);
        this.secretRejected.set(this.candidateSecret !== null);
        this.candidateSecret = null;
      }
    });

    const fromUrl = new URLSearchParams(window.location.search).get('details');
    const candidate = fromUrl ?? read();
    if (candidate) {
      void this.unlock(candidate);
    }

    window.setInterval(() => this.reloadToken.set(Date.now()), RELOAD_INTERVAL_MS);
  }

  async unlock(secret: string): Promise<void> {
    const trimmed = secret.trim();
    if (trimmed.length === 0) {
      return;
    }
    this.candidateSecret = trimmed;
    this.detailsHash.set(await sha256Hex(trimmed));
  }

  lock(): void {
    forget();
    this.candidateSecret = null;
    this.detailsHash.set(null);
  }

  reload(): void {
    this.reloadToken.set(Date.now());
  }
}

async function sha256Hex(value: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(value));
  return Array.from(new Uint8Array(digest))
    .map((byte) => byte.toString(16).padStart(2, '0'))
    .join('');
}

function read(): string | null {
  try {
    return window.localStorage.getItem(SECRET_STORAGE_KEY);
  } catch {
    return null;
  }
}

function store(secret: string): void {
  try {
    window.localStorage.setItem(SECRET_STORAGE_KEY, secret);
  } catch {
    return;
  }
}

function forget(): void {
  try {
    window.localStorage.removeItem(SECRET_STORAGE_KEY);
  } catch {
    return;
  }
}
