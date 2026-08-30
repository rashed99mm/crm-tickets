import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { LocaleStore } from '../i18n/locale.store';
import { PortalApi } from '../portal/portal.api';
import { TicketApi } from '../tickets/ticket.api';
import { CsAttachmentList } from './attachment-list.component';

describe('CsAttachmentList', () => {
  it('loads ticket attachments once for the current input instead of reloading on its own state updates', () => {
    const ticketApi = {
      listAttachments: vi.fn(() =>
        of([
          {
            id: 'att-1',
            originalFileName: 'notes.txt',
            contentType: 'text/plain',
            sizeBytes: 12,
            uploadedByName: 'Dana Support',
            createdAt: '2026-08-29T10:00:00Z',
          },
        ]),
      ),
      downloadAttachment: vi.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        LocaleStore,
        { provide: TicketApi, useValue: ticketApi },
        {
          provide: PortalApi,
          useValue: {
            listTicketAttachments: vi.fn(),
            downloadTicketAttachment: vi.fn(),
          },
        },
      ],
    });

    const fixture = TestBed.createComponent(CsAttachmentList);
    fixture.componentRef.setInput('ticketId', 't-1');
    fixture.componentRef.setInput('mode', 'staff');
    fixture.detectChanges();

    expect(ticketApi.listAttachments).toHaveBeenCalledTimes(1);
    expect(ticketApi.listAttachments).toHaveBeenCalledWith('t-1');
    expect(fixture.componentInstance.attachments()).toHaveLength(1);
  });
});
