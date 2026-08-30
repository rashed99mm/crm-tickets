import { ComponentFixture, TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { CsDataToolbar } from './data-toolbar.component';

describe('CsDataToolbar', () => {
  function render(): ComponentFixture<CsDataToolbar> {
    const fixture = TestBed.createComponent(CsDataToolbar);
    fixture.componentRef.setInput('searchPlaceholder', 'Search tickets');
    fixture.componentRef.setInput('statusOptions', [{ value: 'Open', label: 'Open' }]);
    fixture.componentRef.setInput('sortOptions', [{ value: 'newest', label: 'Newest' }]);
    fixture.detectChanges();
    return fixture;
  }

  it('emits search, status and sort changes', () => {
    const fixture = render();
    const search = vi.fn();
    const status = vi.fn();
    const sort = vi.fn();
    fixture.componentInstance.searchChanged.subscribe(search);
    fixture.componentInstance.statusChanged.subscribe(status);
    fixture.componentInstance.sortChanged.subscribe(sort);

    const el = fixture.nativeElement as HTMLElement;
    const input = el.querySelector('input')!;
    input.value = 'billing';
    input.dispatchEvent(new Event('input'));

    const selects = el.querySelectorAll('select');
    selects[0].value = 'Open';
    selects[0].dispatchEvent(new Event('change'));
    selects[1].value = 'newest';
    selects[1].dispatchEvent(new Event('change'));

    expect(search).toHaveBeenCalledWith('billing');
    expect(status).toHaveBeenCalledWith('Open');
    expect(sort).toHaveBeenCalledWith('newest');
  });
});
