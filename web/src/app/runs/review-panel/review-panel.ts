import { Component, input } from '@angular/core';
import { Review } from '../run-models';

@Component({
  selector: 'app-review-panel',
  imports: [],
  templateUrl: './review-panel.html',
  styleUrl: './review-panel.css'
})
export class ReviewPanel {
  readonly review = input<Review | null>(null);
  readonly pending = input(false);
}
