import { TestBed } from '@angular/core/testing';
import { CsChannelPill } from './channel-pill.component';

describe('CsChannelPill', () => {
  it('AC501: renders supported communication channels with distinct semantic classes', () => {
    const fixture = TestBed.createComponent(CsChannelPill);
    fixture.componentRef.setInput('channel', 'WhatsApp');
    fixture.detectChanges();

    const pill = (fixture.nativeElement as HTMLElement).querySelector('span')!;
    expect(pill.className).toContain('bg-emerald-50');
    expect(pill.textContent).toContain('WhatsApp');
  });

  it('AC501: falls back visibly for unknown channel values', () => {
    const fixture = TestBed.createComponent(CsChannelPill);
    fixture.componentRef.setInput('channel', 'PartnerApi');
    fixture.detectChanges();

    const pill = (fixture.nativeElement as HTMLElement).querySelector('span')!;
    expect(pill.className).toContain('bg-surface-highest');
    expect(pill.textContent).toContain('PartnerApi');
  });
});
