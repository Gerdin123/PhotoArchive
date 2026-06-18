import { Component, Input } from '@angular/core';
import { Image } from '../../../../shared/models/image';
import { Router } from '@angular/router';

@Component({
  selector: 'app-thumbnail-card',
  standalone: false,
  templateUrl: './thumbnail-card.html',
  styleUrl: './thumbnail-card.css',
})
export class ThumbnailCard {
  constructor(private readonly router: Router) {}

  @Input({ required: true }) image!: Image;

  toDetails(): void {
    this.router.navigate(['/image', this.image.id]);
  }
}
