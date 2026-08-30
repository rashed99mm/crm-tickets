import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';

// The scaffolded "should render title" test asserted on Angular's welcome
// page, which this app does not ship. Replaced with coverage of the shell.
describe('App', () => {
  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the portal chrome with a language switcher', async () => {
    const fixture = TestBed.createComponent(App);
    await TestBed.inject(Router).navigateByUrl('/');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('header')).not.toBeNull();
    // Bilingual switching must work for customers too, not just staff.
    expect(el.querySelector('cs-language-switcher')).not.toBeNull();
  });

  it('uses no physical-direction class in its own markup', async () => {
    // Same reasoning as the admin shell: the RTL guard scans .html files and
    // this shell is an inline template.
    const fixture = TestBed.createComponent(App);
    await TestBed.inject(Router).navigateByUrl('/');
    fixture.detectChanges();

    const html = (fixture.nativeElement as HTMLElement).innerHTML;
    for (const banned of ['pl-', 'pr-', 'ml-', 'mr-', 'text-left', 'text-right']) {
      expect(html).not.toContain(banned);
    }
  });
});

