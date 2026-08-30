import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { from, Observable } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

export interface UploadResult {
  readonly id?: string;
  readonly url?: string;
  readonly fileName?: string;
  readonly size?: number;
}

/**
 * Global shared service for managing file uploads and reading media across the application.
 */
@Injectable({ providedIn: 'root' })
export class UploadService {
  private readonly http = inject(HttpClient);

  /**
   * Uploads a file as multipart/form-data to the specified API endpoint.
   */
  upload(file: File, endpoint = '/api/upload', fieldName = 'file'): Observable<UploadResult> {
    const formData = new FormData();
    formData.append(fieldName, file, file.name);
    return this.http.post<UploadResult>(endpoint, formData);
  }

  /**
   * Uploads a profile/avatar image to the server.
   * If standalone upload route is not provided, seamlessly falls back to reading the data URL.
   */
  uploadProfileImage(file: File): Observable<{ url: string }> {
    const formData = new FormData();
    formData.append('file', file, file.name);

    return this.http.post<{ url: string }>('/api/Auth/profile-image', formData).pipe(
      catchError(() => {
        return from(this.readFileAsDataUrl(file)).pipe(
          map((dataUrl) => ({ url: dataUrl })),
        );
      }),
    );
  }

  /**
   * Reads a browser File object as a base64 Data URL string.
   */
  readFileAsDataUrl(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result as string);
      reader.onerror = (err) => reject(err);
      reader.readAsDataURL(file);
    });
  }

  /**
   * Validates file type and size constraints.
   */
  validateImage(
    file: File,
    maxSizeBytes = 5 * 1024 * 1024,
  ): { valid: boolean; error?: string } {
    if (!file.type.startsWith('image/')) {
      return { valid: false, error: 'File must be an image (JPEG, PNG, GIF, WebP, SVG)' };
    }
    if (file.size > maxSizeBytes) {
      return { valid: false, error: 'Image size exceeds maximum limit' };
    }
    return { valid: true };
  }
}
