import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CsIcon, TranslatePipe } from 'common';

@Component({
  selector: 'portal-home',
  imports: [RouterLink, CsIcon, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home.component.html',
})
export default class PortalHomeComponent {}
