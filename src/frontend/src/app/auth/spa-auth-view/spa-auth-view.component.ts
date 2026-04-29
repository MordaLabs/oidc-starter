import { JsonPipe, KeyValuePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { SpaAuthViewService } from './spa-auth-view.service';

@Component({
  selector: 'app-spa-auth-view',
  imports: [JsonPipe, KeyValuePipe],
  templateUrl: './spa-auth-view.component.html',
  styleUrl: './spa-auth-view.component.css',
})
export class SpaAuthViewComponent {
  protected readonly service = inject(SpaAuthViewService);
}
