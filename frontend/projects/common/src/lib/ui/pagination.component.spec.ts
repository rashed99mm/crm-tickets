import { ComponentFixture, TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { CsPagination } from './pagination.component';

describe('CsPagination', () => {
  function render(): ComponentFixture<CsPagination> {
    const fixture = TestBed.createComponent(CsPagination);
    fixture.componentRef.setInput('summary', 'Page 2 of 42 results');
    fixture.componentRef.setInput('page', 2);
    fixture.componentRef.setInput('hasMore', true);
    fixture.detectChanges();
    return fixture;
  }

  it('renders the supplied summary', () => {
    const fixture = render();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Page 2 of 42 results');
  });

  it('emits target page numbers for previous and next', () => {
    const fixture = render();
    const previous = vi.fn();
    const next = vi.fn();
    fixture.componentInstance.previous.subscribe(previous);
    fixture.componentInstance.next.subscribe(next);

    const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll('button');
    buttons[0].click();
    buttons[buttons.length - 1].click();

    expect(previous).toHaveBeenCalledWith(1);
    expect(next).toHaveBeenCalledWith(3);
  });

  it('disables previous on the first page and next when no more rows exist', () => {
    const fixture = render();
    fixture.componentRef.setInput('page', 1);
    fixture.componentRef.setInput('hasMore', false);
    fixture.detectChanges();

    const buttons = (fixture.nativeElement as HTMLElement).querySelectorAll('button');
    expect(buttons[0].hasAttribute('disabled')).toBe(true);
    expect(buttons[buttons.length - 1].hasAttribute('disabled')).toBe(true);
  });

  it('renders numbered pages when a total count is supplied', () => {
    const fixture = render();
    fixture.componentRef.setInput('totalCount', 42);
    fixture.componentRef.setInput('pageSize', 10);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('1');
    expect(text).toContain('2');
    expect(text).toContain('5');
  });
});
