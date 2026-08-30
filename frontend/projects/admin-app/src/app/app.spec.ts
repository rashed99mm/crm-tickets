import { TestBed } from '@angular/core/testing';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  // The scaffolded "should render title" test asserted on Angular's welcome
  // page (an <h1> reading "Hello, admin-app"), which this app does not ship.
  // Task 8 replaces this file's coverage with the shell's own tests.
});
