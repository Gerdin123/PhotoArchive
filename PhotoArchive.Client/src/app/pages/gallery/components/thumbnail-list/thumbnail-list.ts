import { Component, HostListener, OnInit } from '@angular/core';
import { Image } from '../../../../shared/models/image';

@Component({
  selector: 'app-thumbnail-list',
  standalone: false,
  templateUrl: './thumbnail-list.html',
  styleUrl: './thumbnail-list.css',
})
export class ThumbnailList implements OnInit {
  columns = 2;

  readonly items: Image[] = Array.from({ length: 72 }, (_, i) => {
    const day = (i % 28) + 1;

    return {
      id: i,
      title: `Placeholder ${i + 1}`,
      date: new Date(`2003-02-${day.toString().padStart(2, '0')}`),
      url: '/favicon.ico',
      tags: [],
      people: [],
    };
  });

  ngOnInit(): void {
    this.updateColumns();
  }

  @HostListener('window:resize')
  onResize(): void {
    this.updateColumns();
  }

  private updateColumns(): void {
    const estimatedCardWidth = 220;
    const fitByWidth = Math.floor(window.innerWidth / estimatedCardWidth);
    this.columns = Math.max(2, Math.min(20, fitByWidth));
  }
}
