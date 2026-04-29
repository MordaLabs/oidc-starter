import { Component, inject } from '@angular/core';
import { BffAuthViewService } from './bff-auth-view.service';

@Component({
  selector: 'app-bff-auth-view',
  templateUrl: './bff-auth-view.component.html',
  styleUrl: './bff-auth-view.component.css',
})
export class BffAuthViewComponent {
  protected readonly service = inject(BffAuthViewService);
}
