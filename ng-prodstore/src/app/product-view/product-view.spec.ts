import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ProductView } from './product-view';

describe('ProductView', () => {
  let component: ProductView;
  let fixture: ComponentFixture<ProductView>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductView],
    }).compileComponents();

    fixture = TestBed.createComponent(ProductView);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
