import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ProdUpload } from './prod-upload';

describe('ProdUpload', () => {
  let component: ProdUpload;
  let fixture: ComponentFixture<ProdUpload>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProdUpload],
    }).compileComponents();

    fixture = TestBed.createComponent(ProdUpload);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
